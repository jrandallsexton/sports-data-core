using AutoFixture;

using FluentAssertions;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Processing;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring;

public class PickScoringAuditProcessorTests : ApiTestBase<PickScoringAuditProcessor>
{
    private readonly Mock<IProvideContests> _contestClientMock = new();
    private static readonly DateTime FixedUtcNow = new(2026, 6, 16, 2, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OriginalScoredAt = new(2025, 11, 1, 23, 30, 0, DateTimeKind.Utc);

    public PickScoringAuditProcessorTests()
    {
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClientMock.Object);

        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedUtcNow);
    }

    [Fact]
    public async Task Process_WhenStoredMatchesRecomputed_DoesNotWriteAndDoesNotFanOut()
    {
        // Arrange — healthy pick: stored IsCorrect / PointsAwarded match
        // what PickScoringService produces against the current MatchupResult.
        var contestId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();

        var (pick, _, _) = await SeedScoredPickAsync(
            contestId, groupId, winnerId,
            storedIsCorrect: true, storedPoints: 1, wasAts: false,
            franchiseId: winnerId);

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.ContestId, contestId)
            .With(r => r.WinnerFranchiseSeasonId, (Guid?)winnerId)
            .With(r => r.FinalizedUtc, (DateTime?)FixedUtcNow.AddHours(-2))
            .Create();

        _contestClientMock
            .Setup(x => x.GetMatchupResult(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<MatchupResult>(result));

        // Real PickScoringService — easier than mocking the score logic.
        // Reuse the Mocker's already-mocked IDateTimeProvider (FixedUtcNow)
        // so the service sees the same fixed time as the SUT.
        Mocker.Use<IPickScoringService>(new PickScoringService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PickScoringService>.Instance,
            Mocker.Get<IDateTimeProvider>()));

        var sut = Mocker.CreateInstance<PickScoringAuditProcessor>();

        // Act
        await sut.Process(new AuditContestCommand(contestId, Sport.FootballNcaa));

        // Assert — pick fields untouched, no fan-out enqueued.
        var refreshed = await DataContext.UserPicks.FindAsync(pick.Id);
        refreshed!.IsCorrect.Should().BeTrue();
        refreshed.PointsAwarded.Should().Be(1);
        refreshed.ScoredAt.Should().Be(OriginalScoredAt);
        refreshed.ModifiedBy.Should().NotBe(CausationId.Api.PickScoringAuditProcessor);

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IScoreLeagueWeeks>(It.IsAny<Expression<Func<IScoreLeagueWeeks, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenStoredIsWrong_CorrectsInPlaceAndEnqueuesLeagueWeek()
    {
        // Arrange — exact corruption pattern: pick was previously scored as
        // IsCorrect=false / PointsAwarded=0 against Guid.Empty winner. Contest
        // has since finalized with the user's actual pick as the real winner.
        var contestId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();

        var (pick, _, matchup) = await SeedScoredPickAsync(
            contestId, groupId, winnerId,
            storedIsCorrect: false, storedPoints: 0, wasAts: false,
            franchiseId: winnerId);

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.ContestId, contestId)
            .With(r => r.WinnerFranchiseSeasonId, (Guid?)winnerId)
            .With(r => r.FinalizedUtc, (DateTime?)FixedUtcNow.AddHours(-2))
            .Create();

        _contestClientMock
            .Setup(x => x.GetMatchupResult(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<MatchupResult>(result));

        Mocker.Use<IPickScoringService>(new PickScoringService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PickScoringService>.Instance,
            Mocker.Get<IDateTimeProvider>()));

        // Capture the *actual* expression the processor passes to Enqueue so
        // we can assert the league-week payload, not just the call count.
        // Without this the test would pass even if the processor enqueued
        // a wrong leagueId / year / week — exactly the kind of regression
        // that breaks leaderboard rescore fan-out.
        (Guid LeagueId, int SeasonYear, int Week, Guid CorrelationId)? enqueuedCall = null;
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue<IScoreLeagueWeeks>(It.IsAny<Expression<Func<IScoreLeagueWeeks, Task>>>()))
            .Callback<Expression<Func<IScoreLeagueWeeks, Task>>>(expr =>
                enqueuedCall = LeagueWeekScoreCallFromExpression(expr));

        var sut = Mocker.CreateInstance<PickScoringAuditProcessor>();

        // Capture CorrelationId from the command we'll invoke with so the
        // assertion can verify the processor propagates it to the fan-out.
        var command = new AuditContestCommand(contestId, Sport.FootballNcaa);

        // Act
        await sut.Process(command);

        // Assert — pick corrected in place; ScoredAt preserved; ModifiedBy
        // stamped with the audit causation ID; fan-out fired with the right
        // league-week payload.
        var refreshed = await DataContext.UserPicks.FindAsync(pick.Id);
        refreshed!.IsCorrect.Should().BeTrue("clone re-scored to a correct pick");
        refreshed.PointsAwarded.Should().Be(1);
        refreshed.ScoredAt.Should().Be(OriginalScoredAt, "audit preserves the original scoring timestamp");
        refreshed.ModifiedBy.Should().Be(CausationId.Api.PickScoringAuditProcessor);
        refreshed.ModifiedUtc.Should().Be(FixedUtcNow);

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IScoreLeagueWeeks>(It.IsAny<Expression<Func<IScoreLeagueWeeks, Task>>>()),
            Times.Once);
        enqueuedCall.Should().NotBeNull();
        enqueuedCall!.Value.LeagueId.Should().Be(groupId, "fan-out must target the pick's league");
        enqueuedCall.Value.SeasonYear.Should().Be(2025, "seeded matchup is for the 2025 season");
        enqueuedCall.Value.Week.Should().Be(10, "seeded matchup is for week 10");
        enqueuedCall.Value.CorrelationId.Should().Be(command.CorrelationId, "audit must propagate the command's correlation id");
    }

    [Fact]
    public async Task Process_WhenMatchupResultNotFound_ResetsStuckPicksAndEnqueuesLeagueWeek()
    {
        // Arrange — pick is scored, but Producer says NotFound (the
        // FinalizedUtc IS NOT NULL SQL filter is rejecting an unfinalized
        // contest). The exact stuck-pick pattern audit is built for.
        var contestId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var (pick, _, _) = await SeedScoredPickAsync(
            contestId, groupId, winnerId: Guid.NewGuid(),
            storedIsCorrect: false, storedPoints: 0, wasAts: false,
            franchiseId: Guid.NewGuid());

        _contestClientMock
            .Setup(x => x.GetMatchupResult(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<MatchupResult>(default!, ResultStatus.NotFound, []));

        var sut = Mocker.CreateInstance<PickScoringAuditProcessor>();

        // Act
        await sut.Process(new AuditContestCommand(contestId, Sport.FootballNcaa));

        // Assert — pick reset to "unscored" so PickScoringJob can re-do it once
        // the contest enriches; fan-out enqueued so stale league-week
        // aggregates rebuild.
        var refreshed = await DataContext.UserPicks.FindAsync(pick.Id);
        refreshed!.ScoredAt.Should().BeNull();
        refreshed.IsCorrect.Should().BeNull();
        refreshed.PointsAwarded.Should().BeNull();
        refreshed.WasAgainstSpread.Should().BeNull();
        refreshed.ModifiedBy.Should().Be(CausationId.Api.PickScoringAuditProcessor);
        refreshed.ModifiedUtc.Should().Be(FixedUtcNow, "audit must stamp the reset with the deterministic time provider");

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IScoreLeagueWeeks>(It.IsAny<Expression<Func<IScoreLeagueWeeks, Task>>>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_WhenNoScoredPicksForContest_ShortCircuits()
    {
        // Arrange — no scored picks at all. Cron may have enqueued this
        // contest before all picks were deleted; processor should bail
        // before any Producer round-trip.
        var contestId = Guid.NewGuid();

        var sut = Mocker.CreateInstance<PickScoringAuditProcessor>();

        // Act
        await sut.Process(new AuditContestCommand(contestId, Sport.FootballNcaa));

        // Assert — no Producer call, no fan-out.
        _contestClientMock.Verify(
            x => x.GetMatchupResult(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IScoreLeagueWeeks>(It.IsAny<Expression<Func<IScoreLeagueWeeks, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenMatchupResultIsSuccessButFinalizedUtcIsNull_SkipsWithoutWriting()
    {
        // Arrange — defensive: the new SQL filter should prevent this, but
        // if anyone reverts it, audit refuses to overwrite valid picks based
        // on pre-enrichment data.
        var contestId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();

        var (pick, _, _) = await SeedScoredPickAsync(
            contestId, groupId, winnerId,
            storedIsCorrect: true, storedPoints: 1, wasAts: false,
            franchiseId: winnerId);

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.ContestId, contestId)
            .With(r => r.FinalizedUtc, (DateTime?)null)
            .Create();

        _contestClientMock
            .Setup(x => x.GetMatchupResult(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<MatchupResult>(result));

        var sut = Mocker.CreateInstance<PickScoringAuditProcessor>();

        await sut.Process(new AuditContestCommand(contestId, Sport.FootballNcaa));

        var refreshed = await DataContext.UserPicks.FindAsync(pick.Id);
        refreshed!.IsCorrect.Should().BeTrue("audit must not mutate when source data is suspect");
        refreshed.PointsAwarded.Should().Be(1);
        refreshed.ScoredAt.Should().Be(OriginalScoredAt);

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IScoreLeagueWeeks>(It.IsAny<Expression<Func<IScoreLeagueWeeks, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_IgnoresPicksFromOtherSports_EvenWithSameContestId()
    {
        // Defense in depth: even if a caller passes the wrong Sport in the
        // command, the processor's data queries are sport-scoped so an MLB
        // audit can't accidentally rewrite NCAAFB picks (or vice versa).
        // Astronomically unlikely with random Guids, but the user explicitly
        // wanted hard sport isolation per CR review on PR #422.
        var sharedContestId = Guid.NewGuid();
        var ncaaGroupId = Guid.NewGuid();
        var mlbGroupId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();

        // Seed an NCAAFB pick (wrong-sport for our MLB audit run).
        var (ncaaPick, _, _) = await SeedScoredPickAsync(
            sharedContestId, ncaaGroupId, winnerId,
            storedIsCorrect: false, storedPoints: 0, wasAts: false,
            franchiseId: winnerId);

        // Seed an MLB pick that SHOULD be in scope.
        var mlbGroup = Fixture.Build<PickemGroup>()
            .With(g => g.Id, mlbGroupId)
            .With(g => g.Sport, Sport.BaseballMlb)
            .With(g => g.PickType, PickType.StraightUp)
            .With(g => g.UseConfidencePoints, false)
            .Create();
        var mlbMatchup = Fixture.Build<PickemGroupMatchup>()
            .With(m => m.ContestId, sharedContestId)
            .With(m => m.GroupId, mlbGroupId)
            .With(m => m.SeasonYear, 2026)
            .With(m => m.SeasonWeek, 5)
            .Without(m => m.GroupWeek)
            .Create();
        var mlbPick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.ContestId, sharedContestId)
            .With(p => p.PickemGroupId, mlbGroupId)
            .With(p => p.Group, mlbGroup)
            .With(p => p.FranchiseId, (Guid?)winnerId)
            .With(p => p.IsCorrect, (bool?)false)
            .With(p => p.PointsAwarded, 0)
            .With(p => p.WasAgainstSpread, (bool?)false)
            .With(p => p.ScoredAt, (DateTime?)OriginalScoredAt)
            .Create();
        await DataContext.PickemGroups.AddAsync(mlbGroup);
        await DataContext.PickemGroupMatchups.AddAsync(mlbMatchup);
        await DataContext.UserPicks.AddAsync(mlbPick);
        await DataContext.SaveChangesAsync();
        DataContext.ChangeTracker.Clear();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.ContestId, sharedContestId)
            .With(r => r.WinnerFranchiseSeasonId, (Guid?)winnerId)
            .With(r => r.FinalizedUtc, (DateTime?)FixedUtcNow.AddHours(-2))
            .Create();

        _contestClientMock
            .Setup(x => x.GetMatchupResult(sharedContestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<MatchupResult>(result));

        Mocker.Use<IPickScoringService>(new PickScoringService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PickScoringService>.Instance,
            Mocker.Get<IDateTimeProvider>()));

        var sut = Mocker.CreateInstance<PickScoringAuditProcessor>();

        // Act — run the MLB audit. NCAAFB pick is "wrong" relative to the
        // MatchupResult winner, but should NOT be corrected because it
        // belongs to a different sport.
        await sut.Process(new AuditContestCommand(sharedContestId, Sport.BaseballMlb));

        // Assert — NCAAFB pick untouched.
        var refreshedNcaa = await DataContext.UserPicks.FindAsync(ncaaPick.Id);
        refreshedNcaa!.IsCorrect.Should().BeFalse("NCAAFB pick must not be touched by MLB audit");
        refreshedNcaa.PointsAwarded.Should().Be(0);
        refreshedNcaa.ModifiedBy.Should().NotBe(CausationId.Api.PickScoringAuditProcessor);

        // MLB pick gets corrected — proves the audit DID run for the
        // right sport.
        var refreshedMlb = await DataContext.UserPicks.FindAsync(mlbPick.Id);
        refreshedMlb!.IsCorrect.Should().BeTrue();
        refreshedMlb.PointsAwarded.Should().Be(1);
        refreshedMlb.ModifiedBy.Should().Be(CausationId.Api.PickScoringAuditProcessor);
    }

    /// <summary>
    /// Compiles the four argument expressions of a
    /// <c>p =&gt; p.Process(leagueId, seasonYear, week, correlationId)</c>
    /// lambda and returns the captured values. Returns null when the
    /// expression isn't shaped as expected — caller asserts NotBeNull.
    /// Mirrors the pattern in PickScoringJobTests.
    /// </summary>
    private static (Guid LeagueId, int SeasonYear, int Week, Guid CorrelationId)?
        LeagueWeekScoreCallFromExpression(Expression<Func<IScoreLeagueWeeks, Task>> expr)
    {
        if (expr.Body is not MethodCallExpression call) return null;
        if (call.Method.Name != nameof(IScoreLeagueWeeks.Process)) return null;
        if (call.Arguments.Count != 4) return null;

        var leagueId = Expression.Lambda<Func<Guid>>(call.Arguments[0]).Compile()();
        var seasonYear = Expression.Lambda<Func<int>>(call.Arguments[1]).Compile()();
        var week = Expression.Lambda<Func<int>>(call.Arguments[2]).Compile()();
        var correlationId = Expression.Lambda<Func<Guid>>(call.Arguments[3]).Compile()();

        return (leagueId, seasonYear, week, correlationId);
    }

    private async Task<(PickemGroupUserPick pick, PickemGroup group, PickemGroupMatchup matchup)>
        SeedScoredPickAsync(
            Guid contestId,
            Guid groupId,
            Guid winnerId,
            bool storedIsCorrect,
            int? storedPoints,
            bool wasAts,
            Guid franchiseId)
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.Id, groupId)
            .With(g => g.Sport, Sport.FootballNcaa)
            .With(g => g.PickType, PickType.StraightUp)
            .With(g => g.UseConfidencePoints, false)
            .Create();

        // Without(GroupWeek): AutoFixture's auto-populated GroupWeek
        // navigation would cascade-insert a stray PickemGroupWeek and confuse
        // the FK relationship the processor's query relies on.
        var matchup = Fixture.Build<PickemGroupMatchup>()
            .With(m => m.ContestId, contestId)
            .With(m => m.GroupId, groupId)
            .With(m => m.SeasonYear, 2025)
            .With(m => m.SeasonWeek, 10)
            .Without(m => m.GroupWeek)
            .Create();

        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.ContestId, contestId)
            .With(p => p.PickemGroupId, groupId)
            .With(p => p.Group, group)
            .With(p => p.FranchiseId, (Guid?)franchiseId)
            .With(p => p.IsCorrect, (bool?)storedIsCorrect)
            .With(p => p.PointsAwarded, storedPoints)
            .With(p => p.WasAgainstSpread, (bool?)wasAts)
            .With(p => p.ScoredAt, (DateTime?)OriginalScoredAt)
            .Create();

        await DataContext.UserPicks.AddAsync(pick);
        await DataContext.SaveChangesAsync();
        DataContext.ChangeTracker.Clear();

        return (pick, group, matchup);
    }
}
