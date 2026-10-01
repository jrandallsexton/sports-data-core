using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SportsData.Api.Application.Admin.Commands.BackfillUserPickBetPoints;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Commands.BackfillUserPickBetPoints;

public class ApplyUserPickBetPointsHandlerTests : ApiTestBase<ApplyUserPickBetPointsHandler>
{
    private static readonly DateTime Kickoff = new(2026, 9, 26, 19, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime ScoredAt = new(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _contestId = Guid.NewGuid();
    private readonly Guid _homeId = Guid.NewGuid();
    private readonly Guid _awayId = Guid.NewGuid();
    private readonly Mock<IProvideContests> _ncaaClient = new();

    public ApplyUserPickBetPointsHandlerTests()
    {
        Mocker.GetMock<IContestClientFactory>().Setup(x => x.Resolve(Sport.FootballNcaa)).Returns(_ncaaClient.Object);
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
        // The real scoring service: the backfill must compute exactly what scoring does.
        Mocker.Use<IPickScoringService>(new PickScoringService(
            NullLogger<PickScoringService>.Instance,
            Mocker.Get<IDateTimeProvider>()));
    }

    private async Task<Guid> SeedGroupAsync(Sport sport, League league, PickType pickType)
    {
        var group = new PickemGroup
        {
            Id = Guid.NewGuid(), Name = $"{sport} league", Sport = sport, League = league,
            CommissionerUserId = Guid.NewGuid(), PickType = pickType
        };
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(new PickemGroupMatchup
        {
            Id = Guid.NewGuid(), GroupId = group.Id, ContestId = _contestId, SeasonWeekId = Guid.NewGuid(),
            SeasonYear = 2026, SeasonWeek = 5, StartDateUtc = Kickoff,
            AwayMoneyLine = 240, HomeMoneyLine = -300, AwaySpreadPrice = -105, HomeSpreadPrice = -115
        });
        await DataContext.SaveChangesAsync();
        return group.Id;
    }

    private async Task<Guid> SeedPickAsync(Guid groupId, Guid? franchiseSeasonId, DateTime? scoredAt, bool? isCorrect = true, int? pointsAwarded = 1)
    {
        var pick = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(), PickemGroupId = groupId, UserId = Guid.NewGuid(), ContestId = _contestId,
            FranchiseSeasonId = franchiseSeasonId, ScoredAt = scoredAt, IsCorrect = isCorrect, PointsAwarded = pointsAwarded
        };
        await DataContext.UserPicks.AddAsync(pick);
        await DataContext.SaveChangesAsync();
        DataContext.ChangeTracker.Clear();
        return pick.Id;
    }

    /// <summary>Home -7, home wins 27-17: home wins outright and covers.</summary>
    private void ProducerReturnsHomeWin() =>
        ProducerReturns(new Success<MatchupResult>(new MatchupResult
        {
            ContestId = _contestId, HomeFranchiseSeasonId = _homeId, AwayFranchiseSeasonId = _awayId,
            HomeScore = 27, AwayScore = 17, Spread = -7.0, WinnerFranchiseSeasonId = _homeId,
            FinalizedUtc = ScoredAt
        }));

    private void ProducerReturns(Result<MatchupResult> result) =>
        _ncaaClient.Setup(x => x.GetMatchupResult(_contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private Task RunAsync() =>
        Mocker.CreateInstance<ApplyUserPickBetPointsHandler>()
            .Process(new ApplyUserPickBetPointsCommand(Sport.FootballNcaa, _contestId, Guid.NewGuid()));

    private Task<PickemGroupUserPick> LoadAsync(Guid id) =>
        DataContext.UserPicks.AsNoTracking().SingleAsync(p => p.Id == id);

    [Fact]
    public async Task ComputesTheLeaguesBetColumn_AndLeavesLeagueScoringAlone()
    {
        var su = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF, PickType.StraightUp);
        var ats = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF, PickType.AgainstTheSpread);
        // Stored scoring deliberately "wrong": the backfill must not touch it.
        var suPick = await SeedPickAsync(su, _awayId, ScoredAt, isCorrect: true, pointsAwarded: 7);
        var atsPick = await SeedPickAsync(ats, _homeId, ScoredAt, isCorrect: false, pointsAwarded: 0);
        ProducerReturnsHomeWin();

        await RunAsync();

        var suAfter = await LoadAsync(suPick);
        Assert.Equal(-1m, suAfter.PointsSU);
        Assert.Null(suAfter.PointsATS);
        Assert.True(suAfter.IsCorrect);
        Assert.Equal(7, suAfter.PointsAwarded);
        Assert.Equal(ScoredAt, suAfter.ScoredAt);
        Assert.Equal(Now, suAfter.ModifiedUtc);

        var atsAfter = await LoadAsync(atsPick);
        Assert.Equal(0.8696m, atsAfter.PointsATS);
        Assert.Null(atsAfter.PointsSU);
        Assert.False(atsAfter.IsCorrect);
        Assert.Equal(0, atsAfter.PointsAwarded);
    }

    [Fact]
    public async Task SkipsUnscoredPicks()
    {
        var su = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF, PickType.StraightUp);
        var unscored = await SeedPickAsync(su, _homeId, scoredAt: null, isCorrect: null, pointsAwarded: null);
        ProducerReturnsHomeWin();

        await RunAsync();

        var after = await LoadAsync(unscored);
        Assert.Null(after.PointsSU);
        Assert.Null(after.ModifiedUtc);
    }

    [Fact]
    public async Task IgnoresPicksInAnotherSportsLeague()
    {
        var nfl = await SeedGroupAsync(Sport.FootballNfl, League.NFL, PickType.StraightUp);
        var nflPick = await SeedPickAsync(nfl, _homeId, ScoredAt);
        ProducerReturnsHomeWin();

        await RunAsync();

        Assert.Null((await LoadAsync(nflPick)).PointsSU);
    }

    [Fact]
    public async Task ReRun_DoesNotRestamp()
    {
        var su = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF, PickType.StraightUp);
        var pick = await SeedPickAsync(su, _homeId, ScoredAt);
        ProducerReturnsHomeWin();
        await RunAsync();
        DataContext.ChangeTracker.Clear();

        var stamped = await DataContext.UserPicks.SingleAsync(p => p.Id == pick);
        stamped.ModifiedUtc = Kickoff;
        await DataContext.SaveChangesAsync();
        DataContext.ChangeTracker.Clear();

        await RunAsync();

        var after = await LoadAsync(pick);
        Assert.Equal(0.3333m, after.PointsSU);
        Assert.Equal(Kickoff, after.ModifiedUtc);
    }

    [Fact]
    public async Task NotFound_LeavesPicksUnchanged()
    {
        var su = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF, PickType.StraightUp);
        var pick = await SeedPickAsync(su, _homeId, ScoredAt);
        ProducerReturns(new Failure<MatchupResult>(default!, ResultStatus.NotFound, []));

        await RunAsync();

        Assert.Null((await LoadAsync(pick)).PointsSU);
    }

    [Fact]
    public async Task OtherFailure_Throws_SoHangfireRetries()
    {
        var su = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF, PickType.StraightUp);
        await SeedPickAsync(su, _homeId, ScoredAt);
        ProducerReturns(new Failure<MatchupResult>(default!, ResultStatus.Error, [new ValidationFailure("x", "boom")]));

        await Assert.ThrowsAsync<InvalidOperationException>(RunAsync);
    }
}
