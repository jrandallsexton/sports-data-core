using FluentAssertions;

using Moq;

using SportsData.Api.Application;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekOverview;
using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using League = SportsData.Api.Application.League;
using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Queries.GetLeagueWeekOverview;

public class GetLeagueWeekOverviewQueryHandlerTests : ApiTestBase<GetLeagueWeekOverviewQueryHandler>
{
    private readonly Mock<IProvideCanonicalData> _canonicalDataProviderMock;
    private readonly Mock<IGetUserPicksByGroupAndWeekQueryHandler> _userPicksQueryHandlerMock;

    public GetLeagueWeekOverviewQueryHandlerTests()
    {
        _canonicalDataProviderMock = Mocker.GetMock<IProvideCanonicalData>();
        _userPicksQueryHandlerMock = Mocker.GetMock<IGetUserPicksByGroupAndWeekQueryHandler>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenLeagueNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = Guid.NewGuid(),
            Week = 1
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        var failure = result as Failure<LeagueWeekOverviewDto>;
        failure!.Errors.Should().ContainSingle(e => e.PropertyName == nameof(query.LeagueId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoMatchupsInWeek_ReturnsEmptyContests()
    {
        // Arrange
        var user = CreateUser("Test User");
        DataContext.Users.Add(user);

        var league = CreateLeagueWithMember(user.Id);
        DataContext.PickemGroups.Add(league);
        await DataContext.SaveChangesAsync();

        _canonicalDataProviderMock
            .Setup(x => x.GetContestResultsByContestIds(It.IsAny<List<Guid>>()))
            .ReturnsAsync([]);

        _userPicksQueryHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<GetUserPicksByGroupAndWeekQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<UserPickDto>>([]));

        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = league.Id,
            Week = 1
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contests.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenContestHasNoMatchingMatchup_ReturnsBadRequestFailure()
    {
        // Arrange
        var user = CreateUser("Test User");
        DataContext.Users.Add(user);

        var league = CreateLeagueWithMember(user.Id);
        DataContext.PickemGroups.Add(league);

        var contestId = Guid.NewGuid();
        var matchup = CreateMatchup(league.Id, contestId, weekNumber: 5);
        DataContext.PickemGroupMatchups.Add(matchup);
        await DataContext.SaveChangesAsync();

        // Return a contest with a different ContestId (mismatch)
        var differentContestId = Guid.NewGuid();
        _canonicalDataProviderMock
            .Setup(x => x.GetContestResultsByContestIds(It.IsAny<List<Guid>>()))
            .ReturnsAsync([CreateContestResult(differentContestId)]);

        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = league.Id,
            Week = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ReturnsContestsOrderedByStartDate()
    {
        // Arrange
        var user = CreateUser("Test User");
        DataContext.Users.Add(user);

        var league = CreateLeagueWithMember(user.Id);
        DataContext.PickemGroups.Add(league);

        var contestId1 = Guid.NewGuid();
        var contestId2 = Guid.NewGuid();

        var matchup1 = CreateMatchup(league.Id, contestId1, weekNumber: 5);
        var matchup2 = CreateMatchup(league.Id, contestId2, weekNumber: 5);
        DataContext.PickemGroupMatchups.AddRange(matchup1, matchup2);
        await DataContext.SaveChangesAsync();

        var contestResult1 = CreateContestResult(contestId1, startDateUtc: DateTime.UtcNow.AddHours(2));
        var contestResult2 = CreateContestResult(contestId2, startDateUtc: DateTime.UtcNow.AddHours(1)); // Earlier

        _canonicalDataProviderMock
            .Setup(x => x.GetContestResultsByContestIds(It.IsAny<List<Guid>>()))
            .ReturnsAsync([contestResult1, contestResult2]);

        _userPicksQueryHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<GetUserPicksByGroupAndWeekQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<UserPickDto>>([]));

        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = league.Id,
            Week = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contests.Should().HaveCount(2);
        // Should be ordered by start date (contestResult2 first)
        result.Value.Contests[0].ContestId.Should().Be(contestId2);
        result.Value.Contests[1].ContestId.Should().Be(contestId1);
    }

    [Fact]
    public async Task ExecuteAsync_CalculatesSpreadWinnerCorrectly_WhenAwayCoversSpread()
    {
        // Arrange
        var user = CreateUser("Test User");
        DataContext.Users.Add(user);

        var league = CreateLeagueWithMember(user.Id);
        DataContext.PickemGroups.Add(league);

        var contestId = Guid.NewGuid();
        var awayFranchiseSeasonId = Guid.NewGuid();
        var homeFranchiseSeasonId = Guid.NewGuid();

        var matchup = CreateMatchup(league.Id, contestId, weekNumber: 5);
        matchup.AwaySpread = 3.5; // Away is +3.5 underdog
        matchup.HomeSpread = -3.5;
        DataContext.PickemGroupMatchups.Add(matchup);
        await DataContext.SaveChangesAsync();

        // Away loses by 2, but covers the spread (+3.5)
        var contestResult = CreateContestResult(contestId);
        contestResult.AwayScore = 20;
        contestResult.HomeScore = 22;
        contestResult.AwayFranchiseSeasonId = awayFranchiseSeasonId;
        contestResult.HomeFranchiseSeasonId = homeFranchiseSeasonId;

        _canonicalDataProviderMock
            .Setup(x => x.GetContestResultsByContestIds(It.IsAny<List<Guid>>()))
            .ReturnsAsync([contestResult]);

        _userPicksQueryHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<GetUserPicksByGroupAndWeekQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<UserPickDto>>([]));

        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = league.Id,
            Week = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contests.Should().HaveCount(1);
        // Away covered: 20 + 3.5 = 23.5 > 22, so away wins ATS
        result.Value.Contests[0].LeagueWinnerFranchiseSeasonId.Should().Be(awayFranchiseSeasonId);
    }

    [Fact]
    public async Task ExecuteAsync_GathersUserPicksForAllMembers()
    {
        // Arrange
        var user1 = CreateUser("Alpha User");
        var user2 = CreateUser("Beta User");
        DataContext.Users.AddRange(user1, user2);

        var league = CreateLeague();
        league.Members.Add(new PickemGroupMember { UserId = user1.Id, User = user1, Role = LeagueRole.Commissioner });
        league.Members.Add(new PickemGroupMember { UserId = user2.Id, User = user2, Role = LeagueRole.Member });
        DataContext.PickemGroups.Add(league);
        await DataContext.SaveChangesAsync();

        _canonicalDataProviderMock
            .Setup(x => x.GetContestResultsByContestIds(It.IsAny<List<Guid>>()))
            .ReturnsAsync([]);

        var user1Picks = new List<UserPickDto> { new() { UserId = user1.Id, ContestId = Guid.NewGuid() } };
        var user2Picks = new List<UserPickDto> { new() { UserId = user2.Id, ContestId = Guid.NewGuid() } };

        _userPicksQueryHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.Is<GetUserPicksByGroupAndWeekQuery>(q => q.UserId == user1.Id && q.GroupId == league.Id && q.WeekNumber == 5),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<UserPickDto>>(user1Picks));
        _userPicksQueryHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.Is<GetUserPicksByGroupAndWeekQuery>(q => q.UserId == user2.Id && q.GroupId == league.Id && q.WeekNumber == 5),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<UserPickDto>>(user2Picks));

        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = league.Id,
            Week = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserPicks.Should().HaveCount(2);
        result.Value.UserPicks.Should().Contain(p => p.UserId == user1.Id);
        result.Value.UserPicks.Should().Contain(p => p.UserId == user2.Id);
    }

    #region Helper Methods

    private static PickemGroup CreateLeague(string name = "Test League")
    {
        var commissionerId = Guid.NewGuid();
        return new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = name,
            CommissionerUserId = commissionerId,
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            IsPublic = false,
            UseConfidencePoints = false,
            CreatedBy = commissionerId,
            CreatedUtc = DateTime.UtcNow,
            Members = []
        };
    }

    private static PickemGroup CreateLeagueWithMember(Guid userId)
    {
        var league = CreateLeague();
        league.CommissionerUserId = userId;
        league.Members.Add(new PickemGroupMember
        {
            UserId = userId,
            Role = LeagueRole.Commissioner
        });
        return league;
    }

    private static UserEntity CreateUser(string displayName)
    {
        return new UserEntity
        {
            Id = Guid.NewGuid(),
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = $"{displayName.Replace(" ", "").ToLowerInvariant()}@test.com",
            DisplayName = displayName,
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow
        };
    }

    private static PickemGroupMatchup CreateMatchup(Guid groupId, Guid contestId, int weekNumber)
    {
        return new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            SeasonWeekId = Guid.NewGuid(),
            SeasonYear = 2025,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow.AddDays(1),
            CreatedBy = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow
        };
    }

    private static ContestResultDto CreateContestResult(Guid contestId, DateTime? startDateUtc = null)
    {
        return new ContestResultDto
        {
            ContestId = contestId,
            StartDateUtc = startDateUtc ?? DateTime.UtcNow.AddDays(1),
            AwayShort = "AWAY",
            AwaySlug = "away-team",
            AwayFranchiseSeasonId = Guid.NewGuid(),
            HomeShort = "HOME",
            HomeSlug = "home-team",
            HomeFranchiseSeasonId = Guid.NewGuid(),
            AwayScore = 21,
            HomeScore = 24
        };
    }

    #endregion
}
