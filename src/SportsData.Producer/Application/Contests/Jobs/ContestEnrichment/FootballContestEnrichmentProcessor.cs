using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;
using SportsData.Producer.Application.FranchiseContestFacts.Commands.ProjectContestFacts;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Contests.Jobs.ContestEnrichment
{
    public interface IEnrichContests
    {
        Task Process(EnrichContestCommand command);
    }

    public class FootballContestEnrichmentProcessor : IEnrichContests
    {
        private readonly ILogger<FootballContestEnrichmentProcessor> _logger;
        private readonly FootballDataContext _dataContext;
        private readonly IEventBus _bus;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public FootballContestEnrichmentProcessor(
            ILogger<FootballContestEnrichmentProcessor> logger,
            FootballDataContext dataContext,
            IEventBus bus,
            IDateTimeProvider dateTimeProvider,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _bus = bus;
            _dateTimeProvider = dateTimeProvider;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task Process(EnrichContestCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["ContestId"] = command.ContestId,
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                _logger.LogInformation("Contest enrichment job started for {@command}", command);

                var competition = await _dataContext.Competitions
                    .Include(c => c.Competitors)
                    .Include(c => c.Odds)
                    .ThenInclude(o => o.Teams)
                    .Include(c => c.Contest)
                    .Where(c => c.ContestId == command.ContestId)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync();

                if (competition is null)
                {
                    _logger.LogError("Competition could not be loaded for provided contest id. {@Command}", command);
                    return;
                }

                var contestAlreadyFinalized = competition.Contest.FinalizedUtc != null;
                var unfinalizedOdds = competition.Odds?
                    .Where(o => o.FinalizedUtc == null).ToList() ?? new List<CompetitionOdds>();

                if (contestAlreadyFinalized && unfinalizedOdds.Count == 0)
                {
                    _logger.LogInformation(
                        "Contest and all odds rows already finalized. Nothing to do. FinalizedUtc={FinalizedUtc}",
                        competition.Contest.FinalizedUtc);
                    return;
                }

                var awayCompetitor = competition.Competitors.FirstOrDefault(c => c.HomeAway == "away");
                var homeCompetitor = competition.Competitors.FirstOrDefault(c => c.HomeAway == "home");

                if (awayCompetitor is null || homeCompetitor is null)
                {
                    _logger.LogError(
                        "Competition is missing away or home competitor. ContestId={ContestId}, Away={HasAway}, Home={HasHome}",
                        command.ContestId, awayCompetitor is not null, homeCompetitor is not null);
                    return;
                }

                var contest = competition.Contest;

                if (contestAlreadyFinalized)
                {
                    // Odds-late path: Contest finalized earlier, but ≥1
                    // CompetitionOdds row arrived (or stayed unfinalized) after.
                    // Reuse the persisted Contest scores — do NOT re-derive
                    // from CompetitorScores so we don't drift from the values
                    // picks were scored against. Do NOT republish
                    // ContestFinalized — downstream picks scoring already ran;
                    // republishing would double-score.
                    if (contest.AwayScore is null || contest.HomeScore is null)
                    {
                        _logger.LogWarning(
                            "Contest finalized but missing AwayScore/HomeScore — cannot finalize {UnfinalizedOddsCount} late odds row(s). Manual intervention required.",
                            unfinalizedOdds.Count);
                        return;
                    }

                    _logger.LogInformation(
                        "Contest already finalized; running odds-late finalization for {UnfinalizedOddsCount} provider(s).",
                        unfinalizedOdds.Count);

                    EnrichOddsResults(
                        unfinalizedOdds,
                        awayCompetitor.FranchiseSeasonId,
                        homeCompetitor.FranchiseSeasonId,
                        contest.AwayScore.Value,
                        contest.HomeScore.Value);

                    // Refresh Contest-level denorm if a higher-preference
                    // provider just landed — ordered preference, live odds
                    // never eligible (see OddsProviderPreference).
                    var primaryOddsLate = OddsProviderPreference.SelectPrimary(competition.Odds);

                    if (primaryOddsLate != null)
                    {
                        var lateSpreadWinner = ToContestSpreadWinner(primaryOddsLate.AtsWinnerFranchiseSeasonId);
                        var denormChanged =
                            contest.OverUnder != primaryOddsLate.OverUnderResult
                            || contest.SpreadWinnerFranchiseSeasonId != lateSpreadWinner;

                        contest.OverUnder = primaryOddsLate.OverUnderResult;
                        contest.SpreadWinnerFranchiseSeasonId = lateSpreadWinner;

                        if (denormChanged)
                        {
                            _logger.LogWarning(
                                "Contest denorm refreshed by late primary odds. ProviderName={ProviderName}, OverUnderResult={OverUnderResult}, AtsWinner={AtsWinnerFranchiseSeasonId}. Downstream picks NOT re-scored (per-provider scoring is a future refactor).",
                                primaryOddsLate.ProviderName,
                                primaryOddsLate.OverUnderResult,
                                primaryOddsLate.AtsWinnerFranchiseSeasonId);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Primary odds provider unchanged after odds-late finalization. ProviderName={ProviderName}",
                                primaryOddsLate.ProviderName);
                        }
                    }

                    await _dataContext.SaveChangesAsync();

                    // ContestFinalized is (rightly) not republished here, so
                    // the team-game fact rows would keep the old line and ATS
                    // result; re-project them directly (idempotent).
                    var factCmd = new ProjectContestFactsCommand(contest.Id);
                    _backgroundJobProvider.Enqueue<IProjectContestFactsCommandHandler>(
                        h => h.ExecuteAsync(factCmd, CancellationToken.None));

                    _logger.LogInformation(
                        "Odds-late finalization completed. OddsProvidersFinalized={OddsProvidersFinalized}",
                        competition.Odds!.Count(o => o.FinalizedUtc.HasValue));
                    return;
                }

                // Status was lifted off CompetitionBase onto the
                // sport-specific Football/Baseball subclasses. Loaded
                // independently via the abstract base set; EF
                // materializes whichever concrete subtype is registered
                // in the active per-sport context.
                var status = await _dataContext.CompetitionStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.CompetitionId == competition.Id);

                // If canonical status is not final, data isn't ready — return cleanly
                if (status?.StatusTypeName != "STATUS_FINAL")
                {
                    _logger.LogInformation(
                        "Contest status is not yet final for {ContestName}. Current: {Status}. Skipping enrichment.",
                        contest.Name, status?.StatusTypeName ?? "unknown");
                    return;
                }

                // Final score: MAX competitor score per side first, MAX over
                // the scoring plays as the fallback.
                //
                // Competitor score rows are ESPN's own final score. Scoring
                // plays are only as complete as ESPN's play-by-play, which
                // for historical seasons is often partial: a game missing
                // some scoring plays produced a partial score and the wrong
                // winner (prod 2026-11-08: USC at Washington State 2010
                // stored 7-7 against a real 50-16; 2008-2012 had 531
                // contests whose stored score differed from their competitor
                // score). The audit processor derives its expected score
                // from MAX competitor score rows, so reading the same source
                // first keeps both sides agreeing by construction; a play
                // score the audit cannot reproduce re-queues enrichment
                // until the contest is flagged.
                //
                // MAX, not "the latest row": NCAAFB updates its "Basic/Manual"
                // row in place and MLB inserts a separate "feed" row, but in
                // both cases MAX equals the latest known cumulative score.
                // (double?)…MaxAsync() returns null on an empty set, which
                // is what distinguishes "no rows" from a real result.
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

                var hasCompetitorScores = awayMaxScore is not null && homeMaxScore is not null;

                // 0-0 competitor rows are bootstrap-only rows that were
                // never refreshed, not a result: NFL hasn't had a 0-0 final
                // since 1943 and NCAA overtime guarantees a non-tie. Treat
                // them as absent so the scoring plays can supply the score.
                // The audit defers on 0-0 rows against a non-zero stored
                // score, so a play-derived score here is not re-queued.
                var competitorScoresAreBootstrap = hasCompetitorScores
                    && awayMaxScore!.Value == 0
                    && homeMaxScore!.Value == 0;

                string scoreSource;

                if (hasCompetitorScores && !competitorScoresAreBootstrap)
                {
                    contest.AwayScore = (int)awayMaxScore!.Value;
                    contest.HomeScore = (int)homeMaxScore!.Value;
                    scoreSource = "CompetitorMaxScore";
                }
                else
                {
                    // A play's Away/HomeScore is cumulative and never
                    // decreases, so the maximum is the latest score with no
                    // ordering needed. Ordering was an earlier bug: period
                    // DESC then clock ASC has no tie-break, and the clock
                    // does not run between a touchdown and its extra point,
                    // so the database returned either row and lost the PAT.
                    var awayScoringPlayMax = await _dataContext.CompetitionPlays
                        .AsNoTracking()
                        .Where(p => p.CompetitionId == competition.Id && p.ScoringPlay)
                        .Select(p => (int?)p.AwayScore)
                        .MaxAsync();

                    var homeScoringPlayMax = await _dataContext.CompetitionPlays
                        .AsNoTracking()
                        .Where(p => p.CompetitionId == competition.Id && p.ScoringPlay)
                        .Select(p => (int?)p.HomeScore)
                        .MaxAsync();

                    if (awayScoringPlayMax is null || homeScoringPlayMax is null)
                    {
                        if (competitorScoresAreBootstrap)
                        {
                            _logger.LogWarning(
                                "D2 MAX competitor scores read as 0-0 for {ContestName} (no scoring plays either) — implausible. Deferring enrichment.",
                                contest.Name);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "No competitor score rows and no scoring plays for {ContestName}. Deferring to cron sweep.",
                                contest.Name);
                        }

                        return;
                    }

                    contest.AwayScore = awayScoringPlayMax.Value;
                    contest.HomeScore = homeScoringPlayMax.Value;
                    scoreSource = "ScoringPlay";
                }

                _logger.LogInformation(
                    "Final score derived. Source={Source}, Away={AwayScore}, Home={HomeScore}",
                    scoreSource,
                    contest.AwayScore,
                    contest.HomeScore);

                var awayFranchiseSeasonId = awayCompetitor.FranchiseSeasonId;
                var homeFranchiseSeasonId = homeCompetitor.FranchiseSeasonId;

                if (contest.AwayScore != contest.HomeScore)
                {
                    var homeWasWinner = contest.AwayScore < contest.HomeScore;

                    contest.WinnerFranchiseSeasonId =
                        homeWasWinner ?
                        homeFranchiseSeasonId :
                        awayFranchiseSeasonId;
                }

                _logger.LogInformation(
                    "Straight-up winner derived. WinnerFranchiseSeasonId={WinnerFranchiseSeasonId}, IsTie={IsTie}",
                    contest.WinnerFranchiseSeasonId,
                    contest.AwayScore == contest.HomeScore);

                contest.FinalizedUtc = _dateTimeProvider.UtcNow();

                // Enrich results for every odds provider
                if (competition.Odds?.Any() == true)
                {
                    EnrichOddsResults(
                        competition.Odds,
                        awayFranchiseSeasonId,
                        homeFranchiseSeasonId,
                        contest.AwayScore!.Value,
                        contest.HomeScore!.Value);

                    // Maintain Contest-level denormalized fields from the
                    // primary provider — ordered preference, live odds never
                    // eligible (see OddsProviderPreference; Kent-SC 09-05).
                    var primaryOdds = OddsProviderPreference.SelectPrimary(competition.Odds);

                    if (primaryOdds != null)
                    {
                        contest.OverUnder = primaryOdds.OverUnderResult;
                        contest.SpreadWinnerFranchiseSeasonId = ToContestSpreadWinner(primaryOdds.AtsWinnerFranchiseSeasonId);

                        _logger.LogInformation(
                            "Primary odds provider selected. ProviderName={ProviderName}, OverUnderResult={OverUnderResult}, AtsWinner={AtsWinnerFranchiseSeasonId}",
                            primaryOdds.ProviderName,
                            primaryOdds.OverUnderResult,
                            primaryOdds.AtsWinnerFranchiseSeasonId);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "No finalized odds row available; Contest-level ATS / OverUnder not denormalized.");
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "No CompetitionOdds rows present; skipping per-provider enrichment.");
                }

                await _bus.Publish(
                    new ContestFinalized(
                        ContestId: command.ContestId,
                        Ref: null,
                        Sport: contest.Sport,
                        SeasonYear: contest.SeasonYear,
                        CorrelationId: command.CorrelationId,
                        CausationId: Guid.NewGuid(),
                        AwayScore: contest.AwayScore,
                        HomeScore: contest.HomeScore,
                        WinnerFranchiseSeasonId: contest.WinnerFranchiseSeasonId,
                        SpreadWinnerFranchiseSeasonId: contest.SpreadWinnerFranchiseSeasonId,
                        OverUnderResultRaw: (int)contest.OverUnder,
                        CompletedUtc: contest.FinalizedUtc,
                        AwayFranchiseSeasonId: awayFranchiseSeasonId,
                        HomeFranchiseSeasonId: homeFranchiseSeasonId));
                await _dataContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Contest enrichment completed. ContestName={ContestName}, " +
                    "AwayScore={AwayScore}, HomeScore={HomeScore}, WinnerFranchiseSeasonId={WinnerFranchiseSeasonId}, " +
                    "SpreadWinnerFranchiseSeasonId={SpreadWinnerFranchiseSeasonId}, OverUnder={OverUnder}, " +
                    "FinalizedUtc={FinalizedUtc}, OddsProvidersFinalized={OddsProvidersFinalized}",
                    contest.Name,
                    contest.AwayScore,
                    contest.HomeScore,
                    contest.WinnerFranchiseSeasonId,
                    contest.SpreadWinnerFranchiseSeasonId,
                    contest.OverUnder,
                    contest.FinalizedUtc,
                    competition.Odds?.Count(o => o.FinalizedUtc.HasValue) ?? 0);
            }
        }

        internal void EnrichOddsResults(
            ICollection<CompetitionOdds> allOdds,
            Guid awayFranchiseSeasonId,
            Guid homeFranchiseSeasonId,
            int awayScore,
            int homeScore)
        {
            foreach (var odds in allOdds)
            {
                // Reset prior results so reruns don't retain stale values
                odds.WinnerFranchiseSeasonId = null;
                odds.AtsWinnerFranchiseSeasonId = null;
                odds.OverUnderResult = OverUnderResult.None;

                // Straight-up winner
                if (awayScore != homeScore)
                {
                    odds.WinnerFranchiseSeasonId = homeScore > awayScore
                        ? homeFranchiseSeasonId
                        : awayFranchiseSeasonId;
                }

                // Over/Under result
                if (odds.OverUnder.HasValue)
                {
                    odds.OverUnderResult = GetOverUnderResult(awayScore, homeScore, odds.OverUnder.Value);
                }

                // ATS winner
                if (odds.Spread.HasValue)
                {
                    odds.AtsWinnerFranchiseSeasonId = GetSpreadWinnerFranchiseSeasonId(
                        awayFranchiseSeasonId,
                        homeFranchiseSeasonId,
                        awayScore,
                        homeScore,
                        odds.Spread.Value);
                }

                odds.FinalizedUtc = _dateTimeProvider.UtcNow();

                _logger.LogInformation(
                    "Enriched CompetitionOdds {OddsId} for provider {ProviderName}. " +
                    "Winner={WinnerId}, ATS={AtsId}, O/U={OverUnder}",
                    odds.Id, odds.ProviderName,
                    odds.WinnerFranchiseSeasonId,
                    odds.AtsWinnerFranchiseSeasonId,
                    odds.OverUnderResult);
            }
        }


        /// <summary>
        /// The odds ROW uses Guid.Empty as its push sentinel (null there means
        /// "no spread computed"). The Contest denorm and the ContestFinalized
        /// wire use null for push — the event contract documents "null on a
        /// true spread push" — so the sentinel must not leak past this
        /// boundary: it reached clients as an id matching neither team (blank
        /// result row + every ATS pick graded a loss, 2026-09-07 SMU@FSU).
        /// </summary>
        private static Guid? ToContestSpreadWinner(Guid? atsWinnerFranchiseSeasonId)
            => atsWinnerFranchiseSeasonId == Guid.Empty ? null : atsWinnerFranchiseSeasonId;

        internal OverUnderResult GetOverUnderResult(int awayScore, int homeScore, decimal overUnder)
        {
            var total = awayScore + homeScore;

            if (total > overUnder)
                return OverUnderResult.Over;

            if (total < overUnder)
                return OverUnderResult.Under;

            return OverUnderResult.Push;
        }

        internal Guid? GetSpreadWinnerFranchiseSeasonId(
            Guid awayFranchiseSeasonId,
            Guid homeFranchiseSeasonId,
            int awayScore,
            int homeScore,
            decimal spread)
        {
            var adjustedHomeScore = homeScore + spread;
            var margin = adjustedHomeScore - awayScore;

            return margin switch
            {
                > 0 => homeFranchiseSeasonId,
                < 0 => awayFranchiseSeasonId,
                _ => Guid.Empty
            };
        }
    }
}
