using FluentAssertions;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekOverview;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Queries.GetLeagueWeekOverview;

public class GetLeagueWeekOverviewQueryHandlerTests : ApiTestBase<GetLeagueWeekOverviewQueryHandler>
{
    // The lock rule is kickoff − 5 min against the injected clock; every
    // contest start in these tests is expressed relative to FixedNow so the
    // reveal behavior is deterministic.
    private static readonly DateTime FixedNow = new(2026, 9, 7, 18, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IProvideContests> _contestClientMock = new();

    public GetLeagueWeekOverviewQueryHandlerTests()
    {
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClientMock.Object);
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLeagueNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
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

        _contestClientMock
            .Setup(x => x.GetContestResultsByContestIds(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<ContestResultDto>>([]));


        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = league.Id,
            UserId = Guid.NewGuid(),
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
        _contestClientMock
            .Setup(x => x.GetContestResultsByContestIds(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<ContestResultDto>>([CreateContestResult(differentContestId)]));

        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = league.Id,
            UserId = Guid.NewGuid(),
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

        _contestClientMock
            .Setup(x => x.GetContestResultsByContestIds(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<ContestResultDto>>([contestResult1, contestResult2]));

        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = league.Id,
            UserId = Guid.NewGuid(),
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

        _contestClientMock
            .Setup(x => x.GetContestResultsByContestIds(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<ContestResultDto>>([contestResult]));

        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = league.Id,
            UserId = Guid.NewGuid(),
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
    public async Task ExecuteAsync_GathersUserPicksForAllMembers_OnLockedContests()
    {
        // Arrange — one locked contest (kicked off an hour ago); both
        // members picked it; a third member (the caller) did not.
        var user1 = CreateUser("Alpha User");
        var user2 = CreateUser("Beta User");
        var caller = CreateUser("Caller User");
        DataContext.Users.AddRange(user1, user2, caller);

        var league = CreateLeague();
        league.Members.Add(new PickemGroupMember { UserId = user1.Id, User = user1, Role = LeagueRole.Commissioner });
        league.Members.Add(new PickemGroupMember { UserId = user2.Id, User = user2, Role = LeagueRole.Member });
        league.Members.Add(new PickemGroupMember { UserId = caller.Id, User = caller, Role = LeagueRole.Member });
        DataContext.PickemGroups.Add(league);

        var contestId = Guid.NewGuid();
        DataContext.PickemGroupMatchups.Add(CreateMatchup(league.Id, contestId, weekNumber: 5));
        DataContext.UserPicks.AddRange(
            CreatePick(league.Id, user1.Id, contestId, week: 5),
            CreatePick(league.Id, user2.Id, contestId, week: 5));
        await DataContext.SaveChangesAsync();

        _contestClientMock
            .Setup(x => x.GetContestResultsByContestIds(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<ContestResultDto>>(
                [CreateContestResult(contestId, startDateUtc: FixedNow.AddHours(-1))]));

        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = league.Id,
            UserId = caller.Id,
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

    [Fact]
    public async Task ExecuteAsync_HidesOthersPicks_ForUnlockedContests_ButAlwaysReturnsOwn()
    {
        // The reveal rule: another member's pick is visible ONLY once its
        // contest has locked (kickoff − 5 min); the caller always sees their
        // own picks. Before this was enforced server-side, the payload
        // carried every member's un-locked picks and only the web renderer
        // hid them.
        var caller = CreateUser("Caller User");
        var rival = CreateUser("Rival User");
        DataContext.Users.AddRange(caller, rival);

        var league = CreateLeague();
        league.Members.Add(new PickemGroupMember { UserId = caller.Id, User = caller, Role = LeagueRole.Commissioner });
        league.Members.Add(new PickemGroupMember { UserId = rival.Id, User = rival, Role = LeagueRole.Member });
        DataContext.PickemGroups.Add(league);

        // Locked: kicked off an hour ago. Unlocked: kicks off in 6 minutes
        // (one minute outside the −5 lock window).
        var lockedContestId = Guid.NewGuid();
        var unlockedContestId = Guid.NewGuid();
        DataContext.PickemGroupMatchups.AddRange(
            CreateMatchup(league.Id, lockedContestId, weekNumber: 5),
            CreateMatchup(league.Id, unlockedContestId, weekNumber: 5));

        DataContext.UserPicks.AddRange(
            CreatePick(league.Id, caller.Id, lockedContestId, week: 5),
            CreatePick(league.Id, caller.Id, unlockedContestId, week: 5),
            CreatePick(league.Id, rival.Id, lockedContestId, week: 5),
            CreatePick(league.Id, rival.Id, unlockedContestId, week: 5));
        await DataContext.SaveChangesAsync();

        _contestClientMock
            .Setup(x => x.GetContestResultsByContestIds(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<ContestResultDto>>(
            [
                CreateContestResult(lockedContestId, startDateUtc: FixedNow.AddHours(-1)),
                CreateContestResult(unlockedContestId, startDateUtc: FixedNow.AddMinutes(6))
            ]));

        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = league.Id,
            UserId = caller.Id,
            Week = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert — 3 picks: both of the caller's own, but only the rival's
        // locked one. The rival's un-locked pick must NOT be in the payload.
        result.IsSuccess.Should().BeTrue();
        result.Value.UserPicks.Should().HaveCount(3);
        result.Value.UserPicks.Should()
            .NotContain(p => p.UserId == rival.Id && p.ContestId == unlockedContestId,
                "an un-locked pick belonging to another member must never leave the server");
        result.Value.UserPicks.Should().Contain(p => p.UserId == rival.Id && p.ContestId == lockedContestId);
        result.Value.UserPicks.Should().Contain(p => p.UserId == caller.Id && p.ContestId == unlockedContestId);
    }

    [Fact]
    public async Task ExecuteAsync_LockBoundary_RevealsAtExactlyFiveMinutesBeforeKickoff()
    {
        // The boundary is inclusive: StartDateUtc − 5 min <= now → locked.
        var caller = CreateUser("Caller User");
        var rival = CreateUser("Rival User");
        DataContext.Users.AddRange(caller, rival);

        var league = CreateLeague();
        league.Members.Add(new PickemGroupMember { UserId = caller.Id, User = caller, Role = LeagueRole.Commissioner });
        league.Members.Add(new PickemGroupMember { UserId = rival.Id, User = rival, Role = LeagueRole.Member });
        DataContext.PickemGroups.Add(league);

        var contestId = Guid.NewGuid();
        DataContext.PickemGroupMatchups.Add(CreateMatchup(league.Id, contestId, weekNumber: 5));
        DataContext.UserPicks.Add(CreatePick(league.Id, rival.Id, contestId, week: 5));
        await DataContext.SaveChangesAsync();

        _contestClientMock
            .Setup(x => x.GetContestResultsByContestIds(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<ContestResultDto>>(
                [CreateContestResult(contestId, startDateUtc: FixedNow.AddMinutes(5))]));

        var handler = Mocker.CreateInstance<GetLeagueWeekOverviewQueryHandler>();
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = league.Id,
            UserId = caller.Id,
            Week = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contests.Should().ContainSingle().Which.IsLocked.Should().BeTrue();
        result.Value.UserPicks.Should().ContainSingle(p => p.UserId == rival.Id);
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
            Username = displayName.Replace(" ", "").ToLowerInvariant(),
            Id = Guid.NewGuid(),
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = $"{displayName.Replace(" ", "").ToLowerInvariant()}@test.com",
            DisplayName = displayName,
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow
        };
    }

    private static PickemGroupUserPick CreatePick(Guid groupId, Guid userId, Guid contestId, int week)
    {
        return new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = groupId,
            UserId = userId,
            ContestId = contestId,
            Week = week,
            FranchiseSeasonId = Guid.NewGuid(),
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints,
            CreatedBy = userId,
            CreatedUtc = DateTime.UtcNow
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
