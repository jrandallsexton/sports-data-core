using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests;

public interface IAuditContestEnrichment
{
    Task Process(AuditContestEnrichmentCommand command);
}

public record AuditContestEnrichmentCommand(Guid ContestId, Guid CorrelationId);

/// <summary>
/// Per-contest auditor. Verifies that the current Contest row's scores +
/// derived winner match what re-running the enrichment processor would
/// produce now (MAX(Value) per competitor against
/// <c>CompetitionCompetitorScores</c>). Three outcomes:
///   - Match: stamp <c>AuditedUtc</c>, done.
///   - Mismatch: clear <c>FinalizedUtc</c>, enqueue
///     <see cref="IEnrichContests"/> so the hardened enrichment processor
///     re-derives everything (scores, winner, spread winner, over/under,
///     per-provider odds enrichment) and publishes <c>ContestFinalized</c>
///     itself on success. Do NOT stamp <c>AuditedUtc</c> on this path —
///     the next sweep validates after re-enrichment lands.
///   - Stale source (MAX=null on either side or 0-0 sanity): log warning,
///     leave both <c>FinalizedUtc</c> and <c>AuditedUtc</c> intact. Next
///     sweep retries; if the source stays bad, that is the human-eyes
///     signal flagged by the warning.
///
/// Spawned per-contest by <see cref="IContestEnrichmentAuditJob"/> so
/// audits fan out across Hangfire workers instead of one long-running
/// sweep.
/// </summary>
public class ContestEnrichmentAuditProcessor<TDataContext> : IAuditContestEnrichment
    where TDataContext : TeamSportDataContext
{
    /// <summary>
    /// How many times a mismatch may re-queue enrichment before the audit
    /// gives up and flags the contest. Re-enrichment is deterministic, so if
    /// the same derivation has already run twice without satisfying the
    /// audit, the two sources contradict each other and a third round cannot
    /// change that.
    /// </summary>
    private const int MaxAuditAttempts = 3;

    private readonly ILogger<ContestEnrichmentAuditProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ContestEnrichmentAuditProcessor(
        ILogger<ContestEnrichmentAuditProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task Process(AuditContestEnrichmentCommand command)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ContestId"] = command.ContestId,
            ["CorrelationId"] = command.CorrelationId
        });

        var competition = await _dataContext.Competitions
            .Include(c => c.Competitors)
            .Include(c => c.Contest)
            .Where(c => c.ContestId == command.ContestId)
            .FirstOrDefaultAsync();

        if (competition is null)
        {
            _logger.LogWarning("Audit skipped — competition not found.");
            return;
        }

        var contest = competition.Contest;

        // Race-protect against the same contest being un-finalized between
        // the sweep's candidate scan and this per-contest run.
        if (contest.FinalizedUtc is null)
        {
            _logger.LogInformation("Audit skipped — Contest no longer finalized.");
            return;
        }

        var awayCompetitor = competition.Competitors.FirstOrDefault(c => c.HomeAway == "away");
        var homeCompetitor = competition.Competitors.FirstOrDefault(c => c.HomeAway == "home");

        if (awayCompetitor is null || homeCompetitor is null)
        {
            _logger.LogWarning("Audit skipped — Competition missing away or home competitor.");
            return;
        }

        // Same MAX(Value) read pattern the enrichment processor now uses
        // (PR #431). Keeps detection and re-enrichment using identical
        // source semantics so they cannot diverge.
        var awayMaxScore = await _dataContext.CompetitionCompetitorScores
            .AsNoTracking()
            .Where(s => s.CompetitionCompetitorId == awayCompetitor.Id)
            .Select(s => (double?)s.Value)
            .MaxAsync();

        var homeMaxScore = await _dataContext.CompetitionCompetitorScores
            .AsNoTracking()
            .Where(s => s.CompetitionCompetitorId == homeCompetitor.Id)
            .Select(s => (double?)s.Value)
            .MaxAsync();

        // No-rows guard. If either side has no score rows at all, the
        // canonical source cannot validate this contest. Don't stamp
        // AuditedUtc — next sweep retries. Warning is the human-eyes signal
        // if it persists.
        if (awayMaxScore is null || homeMaxScore is null)
        {
            _logger.LogWarning(
                "Audit deferred — no competitor score rows. ContestName={ContestName}",
                contest.Name);
            return;
        }

        // MLB-specific tie guard: extras run until a side leads at the end
        // of an inning, so a tied "final" — whether 0-0 (bootstrap-only,
        // feed hasn't sourced yet) or non-zero (feed cut off mid-game) —
        // can only mean stale data. Football is exempt: NFL hasn't had a
        // 0-0 final since 1943 and ties are otherwise legitimate; NCAA OT
        // rules guarantee a non-tie. Scoping this to MLB matches the
        // underlying reasoning. Originally just the 0-0 case; broadened to
        // all ties after 2026-06-20 Chicago White Sox @ Detroit Tigers
        // finalized 2-2 with null Winner.
        if (contest.Sport == Sport.BaseballMlb
            && awayMaxScore.Value == homeMaxScore.Value)
        {
            _logger.LogWarning(
                "Audit deferred — MLB MAX competitor scores are tied ({Score}-{Score}) (canonical source still stale). ContestName={ContestName}",
                awayMaxScore.Value, homeMaxScore.Value, contest.Name);
            return;
        }

        // Football 0-0 guard, scoped to the DISAGREEMENT case.
        //
        // The MLB branch above catches every tie for baseball. Football ties
        // are legitimate and must stay auditable, and a football contest whose
        // stored score is ALSO 0-0 keeps its existing behaviour (it matches, so
        // it audits normally — see
        // Process_WhenNonMlbMaxScoresAreZeroZero_StampsAuditedUtc).
        //
        // What is never meaningful is 0-0 competitor rows contradicting a real
        // stored score. That is not a tie, it is bootstrap-only score rows that
        // were never refreshed; the enrichment processor already refuses to
        // WRITE a 0-0 football final on the same reasoning (no NFL 0-0 since
        // 1943, NCAA overtime guarantees a non-tie).
        //
        // Left unguarded the audit calls it a mismatch and clears FinalizedUtc,
        // which drops the contest out of team W/L for as long as it stays
        // unfinalized. Prod 2026-09-19: 14 of the 108 contests flagged after
        // three failed attempts were this case — Baltimore at Tennessee stored
        // 16-10 against an expected 0-0 — each burning its whole allowance on a
        // comparison that could never succeed.
        //
        // Deferring leaves AuditedUtc null, so a later sweep re-checks once the
        // score rows arrive.
        var storedIsNonZero = (contest.AwayScore ?? 0) != 0 || (contest.HomeScore ?? 0) != 0;

        if (contest.Sport != Sport.BaseballMlb
            && awayMaxScore.Value == 0
            && homeMaxScore.Value == 0
            && storedIsNonZero)
        {
            _logger.LogWarning(
                "Audit deferred — MAX competitor scores read 0-0 against a non-zero stored score " +
                "(canonical source still stale). ContestId={ContestId}, ContestName={ContestName}, " +
                "Stored: Away={StoredAway}, Home={StoredHome}",
                contest.Id, contest.Name, contest.AwayScore, contest.HomeScore);
            return;
        }

        var expectedAway = (int)awayMaxScore.Value;
        var expectedHome = (int)homeMaxScore.Value;

        // Winner derivation mirrors the enrichment processor's branch:
        // null on a tie, otherwise the franchise season of the higher score.
        Guid? expectedWinner = null;
        if (expectedAway != expectedHome)
        {
            expectedWinner = expectedAway < expectedHome
                ? homeCompetitor.FranchiseSeasonId
                : awayCompetitor.FranchiseSeasonId;
        }

        var scoresMatch = contest.AwayScore == expectedAway && contest.HomeScore == expectedHome;
        var winnerMatches = contest.WinnerFranchiseSeasonId == expectedWinner;

        if (scoresMatch && winnerMatches)
        {
            contest.AuditedUtc = _dateTimeProvider.UtcNow();
            // Clear the counter: a contest can be re-finalized later (the
            // odds-late path re-stamps FinalizedUtc), and a future mismatch
            // deserves its own full allowance rather than inheriting strikes
            // from a problem that has since been fixed.
            contest.AuditAttemptCount = 0;
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation(
                "Audit passed — stamped AuditedUtc. ContestName={ContestName}, AwayScore={Away}, HomeScore={Home}",
                contest.Name, expectedAway, expectedHome);
            return;
        }

        contest.AuditAttemptCount++;

        // Allowance spent: re-enrichment is deterministic, so the same
        // derivation has now failed to satisfy the audit MaxAuditAttempts
        // times and the two sources genuinely contradict each other. Flag for
        // review and, crucially, LEAVE FinalizedUtc ALONE — clearing it again
        // would drop the contest out of team W/L indefinitely for a data
        // problem no retry can fix.
        if (contest.AuditAttemptCount >= MaxAuditAttempts)
        {
            contest.AuditFlaggedUtc = _dateTimeProvider.UtcNow();

            _logger.LogError(
                "Audit FLAGGED after {Attempts} attempts — scoring plays and competitor scores disagree and re-enrichment cannot reconcile them. " +
                "FinalizedUtc left intact. ContestId={ContestId}, ContestName={ContestName}, " +
                "Stored: Away={CurrentAway}, Home={CurrentHome}; Competitors: Away={ExpectedAway}, Home={ExpectedHome}",
                contest.AuditAttemptCount,
                contest.Id,
                contest.Name,
                contest.AwayScore, contest.HomeScore,
                expectedAway, expectedHome);

            await _dataContext.SaveChangesAsync();
            return;
        }

        // Mismatch — clear FinalizedUtc and enqueue re-enrichment. The
        // enrichment processor's early-exit on FinalizedUtc != null is
        // the same guard that would otherwise no-op a re-run. AuditedUtc
        // stays null; next sweep validates after enrichment lands.
        _logger.LogWarning(
            "Audit mismatch — clearing FinalizedUtc and enqueuing re-enrichment. " +
            "ContestName={ContestName}, " +
            "Current: Away={CurrentAway}, Home={CurrentHome}, Winner={CurrentWinner}; " +
            "Expected: Away={ExpectedAway}, Home={ExpectedHome}, Winner={ExpectedWinner}",
            contest.Name,
            contest.AwayScore, contest.HomeScore, contest.WinnerFranchiseSeasonId,
            expectedAway, expectedHome, expectedWinner);

        // Enqueue BEFORE SaveChangesAsync. If Enqueue throws, FinalizedUtc
        // stays in its prior state and the next audit sweep retries cleanly.
        // If Enqueue succeeds but SaveChangesAsync throws, the queued
        // enrichment fires harmlessly — the processor's FinalizedUtc != null
        // early-exit short-circuits it — and the next audit sweep re-attempts
        // the pair. The reverse order would orphan the row in an unfinalized
        // state with no re-enrichment queued; ContestEnrichmentJob would
        // eventually catch it, but that's a weekly cadence — long lag for
        // no benefit.
        contest.FinalizedUtc = null;

        var enrichCmd = new EnrichContestCommand(command.ContestId, command.CorrelationId);
        _backgroundJobProvider.Enqueue<IEnrichContests>(p => p.Process(enrichCmd));

        await _dataContext.SaveChangesAsync();
    }
}
