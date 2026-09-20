using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests;

public class ContestEnrichmentAuditProcessorTests
    : ProducerTestBase<ContestEnrichmentAuditProcessor<FootballDataContext>>
{
    private static readonly DateTime FixedNow =
        new(2026, 6, 19, 6, 0, 0, DateTimeKind.Utc);

    private static readonly Guid AwayFranchiseSeasonId = Guid.NewGuid();
    private static readonly Guid HomeFranchiseSeasonId = Guid.NewGuid();

    private readonly ContestEnrichmentAuditProcessor<FootballDataContext> _sut;

    public ContestEnrichmentAuditProcessorTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);

        _sut = Mocker.CreateInstance<ContestEnrichmentAuditProcessor<FootballDataContext>>();
    }

    [Fact]
    public async Task Process_WhenScoresAndWinnerMatch_StampsAuditedUtc()
    {
        var (contestId, _, away, home) = await SeedFinalizedContestAsync(
            currentAway: 14, currentHome: 21,
            currentWinner: HomeFranchiseSeasonId);
        await SeedScoreAsync(away.Id, value: 14);
        await SeedScoreAsync(home.Id, value: 21);

        await _sut.Process(new AuditContestEnrichmentCommand(contestId, Guid.NewGuid()));

        var contest = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        contest.AuditedUtc.Should().Be(FixedNow);
        contest.FinalizedUtc.Should().NotBeNull();

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenScoresMismatch_ClearsFinalizedUtcAndEnqueuesReenrichment()
    {
        // The 2026-06-18 Rockies @ Cubs corruption signature: Contest carries
        // stale 0-0 scores + null Winner, but MAX(CCS) shows the real 6-8.
        var (contestId, _, away, home) = await SeedFinalizedContestAsync(
            currentAway: 0, currentHome: 0,
            currentWinner: null);
        await SeedScoreAsync(away.Id, value: 6);
        await SeedScoreAsync(home.Id, value: 8);

        var enqueuedContestIds = new List<Guid>();
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()))
            .Callback<Expression<Func<IEnrichContests, Task>>>(expr =>
                enqueuedContestIds.Add(EnrichContestIdFromExpression(expr) ?? Guid.Empty));

        await _sut.Process(new AuditContestEnrichmentCommand(contestId, Guid.NewGuid()));

        var contest = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        contest.FinalizedUtc.Should().BeNull();
        contest.AuditedUtc.Should().BeNull();

        enqueuedContestIds.Should().ContainSingle().Which.Should().Be(contestId);
        contest.AuditAttemptCount.Should().Be(1);
        contest.AuditFlaggedUtc.Should().BeNull();
    }

    /// <summary>
    /// Re-enrichment is deterministic, so a mismatch that survives it is two
    /// sources contradicting each other, not a transient fault. Prod
    /// 2026-09-18: 37 contests mismatched in consecutive sweeps after being
    /// re-enriched in between — scoring plays vs competitor scores disagreeing
    /// on magnitude (Nevada at UNLV: 37-3 vs 31-3) or transposed outright
    /// (Army at Texas A&amp;M: 28-24 vs 24-28). Before the allowance those
    /// cleared FinalizedUtc forever, which also kept dropping them out of team
    /// W/L, since only finalized contests count toward a record.
    /// </summary>
    [Fact]
    public async Task Process_WhenMismatchPersistsPastTheAllowance_FlagsForReviewAndKeepsFinalizedUtc()
    {
        var (contestId, _, away, home) = await SeedFinalizedContestAsync(
            currentAway: 37, currentHome: 3,
            currentWinner: null);
        await SeedScoreAsync(away.Id, value: 31);
        await SeedScoreAsync(home.Id, value: 3);

        // Two strikes already spent; this call is the third.
        var seeded = await FootballDataContext.Contests.FirstAsync(c => c.Id == contestId);
        seeded.AuditAttemptCount = 2;
        var finalizedBefore = seeded.FinalizedUtc;
        await FootballDataContext.SaveChangesAsync();

        var enqueued = 0;
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()))
            .Callback(() => enqueued++);

        await _sut.Process(new AuditContestEnrichmentCommand(contestId, Guid.NewGuid()));

        var contest = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        contest.AuditAttemptCount.Should().Be(3);
        contest.AuditFlaggedUtc.Should().NotBeNull();
        // The point of flagging: stop dropping it out of team W/L.
        contest.FinalizedUtc.Should().Be(finalizedBefore);
        contest.AuditedUtc.Should().BeNull("flagged is not validated");
        enqueued.Should().Be(0, "re-enrichment cannot reconcile contradictory sources");
    }

    /// <summary>
    /// A contest can be re-finalized later (the odds-late path re-stamps
    /// FinalizedUtc). A future mismatch deserves its own full allowance rather
    /// than inheriting strikes from a problem that has since been fixed.
    /// </summary>
    [Fact]
    public async Task Process_WhenAuditPasses_ResetsTheAttemptCount()
    {
        var (contestId, _, away, home) = await SeedFinalizedContestAsync(
            currentAway: 21, currentHome: 14,
            currentWinner: null);
        await SeedScoreAsync(away.Id, value: 21);
        await SeedScoreAsync(home.Id, value: 14);

        var seeded = await FootballDataContext.Contests.FirstAsync(c => c.Id == contestId);
        seeded.AuditAttemptCount = 2;
        seeded.WinnerFranchiseSeasonId = away.FranchiseSeasonId;
        await FootballDataContext.SaveChangesAsync();

        await _sut.Process(new AuditContestEnrichmentCommand(contestId, Guid.NewGuid()));

        var contest = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        contest.AuditedUtc.Should().NotBeNull();
        contest.AuditAttemptCount.Should().Be(0);
        contest.AuditFlaggedUtc.Should().BeNull();
    }

    [Fact]
    public async Task Process_WhenScoresMatchButWinnerWrong_ClearsFinalizedUtcAndEnqueuesReenrichment()
    {
        // Catches the specific 6-8/null-Winner shape: Contest scores are
        // already correct (consumer chain caught up), but WinnerFranchiseSeasonId
        // wasn't set because the original enrichment skipped the branch.
        var (contestId, _, away, home) = await SeedFinalizedContestAsync(
            currentAway: 6, currentHome: 8,
            currentWinner: null);
        await SeedScoreAsync(away.Id, value: 6);
        await SeedScoreAsync(home.Id, value: 8);

        await _sut.Process(new AuditContestEnrichmentCommand(contestId, Guid.NewGuid()));

        var contest = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        contest.FinalizedUtc.Should().BeNull();
        contest.AuditedUtc.Should().BeNull();

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_WhenNoCompetitorScoreRows_DefersAndLeavesContestUntouched()
    {
        var (contestId, _, _, _) = await SeedFinalizedContestAsync(
            currentAway: 14, currentHome: 21,
            currentWinner: HomeFranchiseSeasonId);
        // Intentionally no score rows seeded.

        await _sut.Process(new AuditContestEnrichmentCommand(contestId, Guid.NewGuid()));

        var contest = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        contest.FinalizedUtc.Should().NotBeNull();
        contest.AuditedUtc.Should().BeNull();

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenMlbMaxScoresAreZeroZero_DefersAndLeavesContestUntouched()
    {
        // Bootstrap-only race — only the basic/manual placeholder rows exist
        // (Value=0). MLB cannot end 0-0 in regulation, so the audit cannot
        // verify a 0-0 state and defers; next sweep retries once the
        // canonical feed lands.
        var (contestId, _, away, home) = await SeedFinalizedContestAsync(
            currentAway: 0, currentHome: 0,
            currentWinner: null,
            sport: Sport.BaseballMlb);
        await SeedScoreAsync(away.Id, value: 0);
        await SeedScoreAsync(home.Id, value: 0);

        await _sut.Process(new AuditContestEnrichmentCommand(contestId, Guid.NewGuid()));

        var contest = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        contest.FinalizedUtc.Should().NotBeNull();
        contest.AuditedUtc.Should().BeNull();

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenMlbMaxScoresAreNonZeroTie_DefersAndLeavesContestUntouched()
    {
        // Mid-game feed cut-off race — the canonical feed sourced through
        // a few innings and stopped, so MAX(CCS) reads as a non-zero tie
        // (e.g. the 2026-06-20 White Sox @ Tigers incident: MAX returned
        // 2-2 because the feed only had the first two innings sourced).
        // MLB cannot end tied in regulation, so the audit cannot verify a
        // tied state and defers; next sweep retries once the feed lands
        // the deciding inning.
        var (contestId, _, away, home) = await SeedFinalizedContestAsync(
            currentAway: 2, currentHome: 2,
            currentWinner: null,
            sport: Sport.BaseballMlb);
        await SeedScoreAsync(away.Id, value: 2);
        await SeedScoreAsync(home.Id, value: 2);

        await _sut.Process(new AuditContestEnrichmentCommand(contestId, Guid.NewGuid()));

        var contest = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        contest.FinalizedUtc.Should().NotBeNull();
        contest.AuditedUtc.Should().BeNull();

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenNonMlbMaxScoresAreZeroZero_StampsAuditedUtc()
    {
        // Football is exempt from the MLB-specific 0-0 guard. A theoretical
        // 0-0 football final passes through and gets audited normally — if
        // current scores and (null) winner match expected, stamp AuditedUtc.
        // Codifies the sport-scoped guard intent so a future change to
        // tighten the universal check would be caught here.
        var (contestId, _, away, home) = await SeedFinalizedContestAsync(
            currentAway: 0, currentHome: 0,
            currentWinner: null,
            sport: Sport.FootballNcaa);
        await SeedScoreAsync(away.Id, value: 0);
        await SeedScoreAsync(home.Id, value: 0);

        await _sut.Process(new AuditContestEnrichmentCommand(contestId, Guid.NewGuid()));

        var contest = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        contest.FinalizedUtc.Should().NotBeNull();
        contest.AuditedUtc.Should().Be(FixedNow);

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Never);
    }

    /// <summary>
    /// 0-0 competitor rows contradicting a REAL stored score are not a tie —
    /// they are bootstrap-only rows that were never refreshed. Treating that as
    /// a mismatch clears FinalizedUtc, which drops the contest out of team W/L
    /// for as long as it stays unfinalized, and no amount of re-enrichment can
    /// reconcile it. Prod 2026-09-19: 14 of the 108 flagged contests were this
    /// case (Baltimore at Tennessee, stored 16-10, expected 0-0), each burning
    /// its whole three-attempt allowance on a comparison that could not succeed.
    /// </summary>
    [Fact]
    public async Task Process_WhenFootballScoresAreZeroZeroAgainstANonZeroStoredScore_DefersWithoutUnfinalizing()
    {
        var (contestId, _, away, home) = await SeedFinalizedContestAsync(
            currentAway: 16, currentHome: 10,
            currentWinner: AwayFranchiseSeasonId,
            sport: Sport.FootballNfl);
        await SeedScoreAsync(away.Id, value: 0);
        await SeedScoreAsync(home.Id, value: 0);

        await _sut.Process(new AuditContestEnrichmentCommand(contestId, Guid.NewGuid()));

        var contest = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);

        // The whole point: the contest keeps counting toward team records.
        contest.FinalizedUtc.Should().NotBeNull();
        contest.AwayScore.Should().Be(16);
        // Not validated either — a later sweep re-checks once the rows arrive.
        contest.AuditedUtc.Should().BeNull();
        // And no strike against the allowance, so it never reaches the flag.
        contest.AuditAttemptCount.Should().Be(0);
        contest.AuditFlaggedUtc.Should().BeNull();

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Never,
            "re-enrichment cannot conjure score rows that were never sourced");
    }

    [Fact]
    public async Task Process_WhenContestNoLongerFinalized_SkipsCleanly()
    {
        // Race protection: the sweep saw FinalizedUtc != null when it built
        // the batch, but by the time this audit ran something cleared it
        // (the live event-driven path being most likely). Skip — let the
        // ContestEnrichmentJob handle the unfinalized state.
        var (contestId, _, away, home) = await SeedFinalizedContestAsync(
            currentAway: 14, currentHome: 21,
            currentWinner: HomeFranchiseSeasonId);

        var contestToUnfinalize = await FootballDataContext.Contests.FirstAsync(c => c.Id == contestId);
        contestToUnfinalize.FinalizedUtc = null;
        await FootballDataContext.SaveChangesAsync();

        await SeedScoreAsync(away.Id, value: 14);
        await SeedScoreAsync(home.Id, value: 21);

        await _sut.Process(new AuditContestEnrichmentCommand(contestId, Guid.NewGuid()));

        var contest = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        contest.AuditedUtc.Should().BeNull();

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenCompetitionNotFound_SkipsCleanly()
    {
        await _sut.Process(new AuditContestEnrichmentCommand(Guid.NewGuid(), Guid.NewGuid()));

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Never);
    }

    private async Task<(Guid ContestId, Guid CompetitionId, FootballCompetitionCompetitor Away, FootballCompetitionCompetitor Home)>
        SeedFinalizedContestAsync(
            int? currentAway,
            int? currentHome,
            Guid? currentWinner,
            Sport sport = Sport.FootballNcaa)
    {
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();

        var contest = new FootballContest
        {
            Id = contestId,
            Name = $"Contest {Guid.NewGuid():N}",
            ShortName = "TC",
            Sport = sport,
            SeasonYear = 2025,
            StartDateUtc = FixedNow.AddDays(-1),
            HomeTeamFranchiseSeasonId = HomeFranchiseSeasonId,
            AwayTeamFranchiseSeasonId = AwayFranchiseSeasonId,
            FinalizedUtc = FixedNow.AddHours(-12),
            AwayScore = currentAway,
            HomeScore = currentHome,
            WinnerFranchiseSeasonId = currentWinner,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };

        var away = new FootballCompetitionCompetitor
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            FranchiseSeasonId = AwayFranchiseSeasonId,
            HomeAway = "away",
            Order = 0
        };
        var home = new FootballCompetitionCompetitor
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            FranchiseSeasonId = HomeFranchiseSeasonId,
            HomeAway = "home",
            Order = 1
        };

        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            Contest = contest,
            Competitors = new List<CompetitionCompetitorBase> { away, home }
        };

        FootballDataContext.Contests.Add(contest);
        FootballDataContext.Competitions.Add(competition);
        await FootballDataContext.SaveChangesAsync();

        return (contestId, competitionId, away, home);
    }

    private async Task SeedScoreAsync(Guid competitionCompetitorId, double value)
    {
        FootballDataContext.CompetitionCompetitorScores.Add(new CompetitionCompetitorScore
        {
            Id = Guid.NewGuid(),
            CompetitionCompetitorId = competitionCompetitorId,
            Value = value,
            DisplayValue = value.ToString(),
            Winner = false,
            SourceId = "2",
            SourceDescription = "feed"
        });
        await FootballDataContext.SaveChangesAsync();
    }

    private static Guid? EnrichContestIdFromExpression(
        Expression<Func<IEnrichContests, Task>> expr)
    {
        if (expr.Body is not MethodCallExpression call) return null;
        if (call.Method.Name != nameof(IEnrichContests.Process)) return null;
        if (call.Arguments.Count != 1) return null;

        var cmd = Expression.Lambda<Func<EnrichContestCommand>>(call.Arguments[0]).Compile()();
        return cmd.ContestId;
    }
}
