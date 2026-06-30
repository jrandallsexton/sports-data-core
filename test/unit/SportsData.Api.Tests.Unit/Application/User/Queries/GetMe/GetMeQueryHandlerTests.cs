using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Moq;

using SportsData.Api.Application;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.User.Dtos;
using SportsData.Api.Application.User.Queries.GetMe;
using SportsData.Api.Config;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.User.Queries.GetMe;

public class GetMeQueryHandlerTests : ApiTestBase<GetMeQueryHandler>
{
    private static readonly Guid SystemUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly DateTime FixedNow = new(2024, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    public GetMeQueryHandlerTests()
    {
        var apiConfig = new ApiConfig
        {
            BaseUrl = "http://localhost:5262",
            UserIdSystem = SystemUserId
        };
        Mocker.GetMock<IOptions<ApiConfig>>()
            .Setup(x => x.Value)
            .Returns(apiConfig);

        // Fixed "now" for the CurrentSeasonWeek projection. Tests that care
        // about the field seed matchups relative to FixedNow.
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);
    }
    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetMeQueryHandler>();
        var query = new GetMeQuery { UserId = Guid.NewGuid() };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Should().BeOfType<Failure<UserDto>>();
        ((Failure<UserDto>)result).Errors.Should().Contain(e => e.PropertyName == "UserId");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Username = "test_user_25",
            Id = userId,
            FirebaseUid = "firebase-123",
            Email = "test@example.com",
            DisplayName = "Test User",
            SignInProvider = "google.com",
            EmailVerified = true,
            IsAdmin = false,
            IsReadOnly = false,
            CreatedUtc = DateTime.UtcNow.AddDays(-30),
            LastLoginUtc = DateTime.UtcNow
        };

        await DataContext.Users.AddAsync(user);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetMeQueryHandler>();
        var query = new GetMeQuery { UserId = userId };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(userId);
        result.Value.Email.Should().Be("test@example.com");
        result.Value.DisplayName.Should().Be("Test User");
        result.Value.FirebaseUid.Should().Be("firebase-123");
        result.Value.IsAdmin.Should().BeFalse();
        result.Value.IsReadOnly.Should().BeFalse();
        result.Value.Leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUserWithLeagues_WhenUserHasMemberships()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var weekId = Guid.NewGuid();

        var user = new UserEntity
        {
            Username = "test_user_26",
            Id = userId,
            FirebaseUid = "firebase-456",
            Email = "league@example.com",
            DisplayName = "League User",
            SignInProvider = "password",
            EmailVerified = false,
            CreatedUtc = DateTime.UtcNow.AddDays(-30),
            LastLoginUtc = DateTime.UtcNow
        };

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };

        var week = new PickemGroupWeek
        {
            Id = weekId,
            GroupId = groupId,
            SeasonYear = 2024,
            SeasonWeek = 5,
            SeasonWeekId = Guid.NewGuid(),
            Group = group
        };

        var membership = new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = groupId,
            User = user,
            Group = group
        };

        group.Weeks.Add(week);
        user.GroupMemberships.Add(membership);

        await DataContext.Users.AddAsync(user);
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupWeeks.AddAsync(week);
        await DataContext.PickemGroupMembers.AddAsync(membership);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetMeQueryHandler>();
        var query = new GetMeQuery { UserId = userId };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Leagues.Should().HaveCount(1);
        result.Value.Leagues.First().Id.Should().Be(groupId);
        result.Value.Leagues.First().Name.Should().Be("Test League");
        result.Value.Leagues.First().SeasonWeeks.Should().Equal(5);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSeasonWeeksAscendingAndDistinct_WhenMultipleWeeksExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var user = new UserEntity
        {
            Username = "test_user_27",
            Id = userId,
            FirebaseUid = "firebase-789",
            Email = "weeks@example.com",
            DisplayName = "Weeks User",
            SignInProvider = "password",
            EmailVerified = false,
            CreatedUtc = DateTime.UtcNow.AddDays(-30),
            LastLoginUtc = DateTime.UtcNow
        };

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Multi-Week League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };

        var week1 = new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonYear = 2024,
            SeasonWeek = 3,
            SeasonWeekId = Guid.NewGuid(),
            Group = group
        };

        var week2 = new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonYear = 2024,
            SeasonWeek = 7,
            SeasonWeekId = Guid.NewGuid(),
            Group = group
        };

        var week3 = new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonYear = 2024,
            SeasonWeek = 5,
            SeasonWeekId = Guid.NewGuid(),
            Group = group
        };

        // Duplicate SeasonWeek=5 row — distinct Id / SeasonWeekId but same week
        // number. Mirrors the real-world case where a group can have multiple
        // PickemGroupWeek rows for the same week (e.g. preseason and regular
        // season both numbered 1, or rows carried forward across SeasonYears).
        // Handler must dedupe; expected output remains [3, 5, 7].
        var week4 = new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonYear = 2024,
            SeasonWeek = 5,
            SeasonWeekId = Guid.NewGuid(),
            Group = group
        };

        var membership = new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = groupId,
            User = user,
            Group = group
        };

        group.Weeks.Add(week1);
        group.Weeks.Add(week2);
        group.Weeks.Add(week3);
        group.Weeks.Add(week4);
        user.GroupMemberships.Add(membership);

        await DataContext.Users.AddAsync(user);
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupWeeks.AddRangeAsync([week1, week2, week3, week4]);
        await DataContext.PickemGroupMembers.AddAsync(membership);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetMeQueryHandler>();
        var query = new GetMeQuery { UserId = userId };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert — 4 input rows [3, 7, 5, 5] → 3 output items, sorted + deduped.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Leagues.Should().HaveCount(1);
        result.Value.Leagues.First().SeasonWeeks.Should().Equal(3, 5, 7);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnCurrentSeasonWeek_AsSmallestWeekWithUnstartedMatchup()
    {
        // Three weeks, each with one matchup. Week 3 is past, Week 5 is mid-window
        // (already started), Week 7 is future. Expected CurrentSeasonWeek = 7 —
        // the earliest week that still has an unstarted game from "now"'s vantage.
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var user = new UserEntity
        {
            Username = "test_user_28",
            Id = userId,
            FirebaseUid = "firebase-current-week",
            Email = "currentweek@example.com",
            DisplayName = "Current Week User",
            SignInProvider = "password",
            EmailVerified = false,
            CreatedUtc = FixedNow.AddDays(-60),
            LastLoginUtc = FixedNow
        };

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Current Week League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };

        var week3 = new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonYear = 2024,
            SeasonWeek = 3,
            SeasonWeekId = Guid.NewGuid(),
            Group = group
        };
        var week5 = new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonYear = 2024,
            SeasonWeek = 5,
            SeasonWeekId = Guid.NewGuid(),
            Group = group
        };
        var week7 = new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonYear = 2024,
            SeasonWeek = 7,
            SeasonWeekId = Guid.NewGuid(),
            Group = group
        };

        var membership = new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = groupId,
            User = user,
            Group = group
        };

        group.Weeks.Add(week3);
        group.Weeks.Add(week5);
        group.Weeks.Add(week7);
        user.GroupMemberships.Add(membership);

        await DataContext.Users.AddAsync(user);
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupWeeks.AddRangeAsync([week3, week5, week7]);
        await DataContext.PickemGroupMembers.AddAsync(membership);

        // One matchup per week, dated relative to FixedNow.
        await DataContext.PickemGroupMatchups.AddRangeAsync([
            new PickemGroupMatchup
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                SeasonWeekId = week3.SeasonWeekId,
                ContestId = Guid.NewGuid(),
                StartDateUtc = FixedNow.AddDays(-21),
                SeasonYear = 2024,
                SeasonWeek = 3
            },
            new PickemGroupMatchup
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                SeasonWeekId = week5.SeasonWeekId,
                ContestId = Guid.NewGuid(),
                StartDateUtc = FixedNow.AddHours(-2),
                SeasonYear = 2024,
                SeasonWeek = 5
            },
            new PickemGroupMatchup
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                SeasonWeekId = week7.SeasonWeekId,
                ContestId = Guid.NewGuid(),
                StartDateUtc = FixedNow.AddDays(10),
                SeasonYear = 2024,
                SeasonWeek = 7
            }
        ]);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetMeQueryHandler>();
        var result = await handler.ExecuteAsync(new GetMeQuery { UserId = userId });

        result.IsSuccess.Should().BeTrue();
        result.Value.Leagues.First().CurrentSeasonWeek.Should().Be(7);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFallBackToMaxSeasonWeek_WhenAllMatchupsArePast()
    {
        // Season-over case — every matchup is in the past. CurrentSeasonWeek
        // should land on MAX(SeasonWeek) (= 7) instead of null so the picks
        // page has somewhere meaningful to display.
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var user = new UserEntity
        {
            Username = "test_user_29",
            Id = userId,
            FirebaseUid = "firebase-season-over",
            Email = "seasonover@example.com",
            DisplayName = "Season Over User",
            SignInProvider = "password",
            EmailVerified = false,
            CreatedUtc = FixedNow.AddDays(-120),
            LastLoginUtc = FixedNow
        };

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Season Over League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };

        var week3 = new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonYear = 2024,
            SeasonWeek = 3,
            SeasonWeekId = Guid.NewGuid(),
            Group = group
        };
        var week7 = new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonYear = 2024,
            SeasonWeek = 7,
            SeasonWeekId = Guid.NewGuid(),
            Group = group
        };

        var membership = new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = groupId,
            User = user,
            Group = group
        };

        group.Weeks.Add(week3);
        group.Weeks.Add(week7);
        user.GroupMemberships.Add(membership);

        await DataContext.Users.AddAsync(user);
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupWeeks.AddRangeAsync([week3, week7]);
        await DataContext.PickemGroupMembers.AddAsync(membership);

        await DataContext.PickemGroupMatchups.AddRangeAsync([
            new PickemGroupMatchup
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                SeasonWeekId = week3.SeasonWeekId,
                ContestId = Guid.NewGuid(),
                StartDateUtc = FixedNow.AddDays(-30),
                SeasonYear = 2024,
                SeasonWeek = 3
            },
            new PickemGroupMatchup
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                SeasonWeekId = week7.SeasonWeekId,
                ContestId = Guid.NewGuid(),
                StartDateUtc = FixedNow.AddDays(-3),
                SeasonYear = 2024,
                SeasonWeek = 7
            }
        ]);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetMeQueryHandler>();
        var result = await handler.ExecuteAsync(new GetMeQuery { UserId = userId });

        result.IsSuccess.Should().BeTrue();
        result.Value.Leagues.First().CurrentSeasonWeek.Should().Be(7);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMarkAsAdmin_WhenUserIsHardcodedAdmin()
    {
        // Arrange
        var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var user = new UserEntity
        {
            Username = "test_user_30",
            Id = adminId,
            FirebaseUid = "firebase-admin",
            Email = "admin@example.com",
            DisplayName = "Admin User",
            SignInProvider = "password",
            EmailVerified = true,
            IsAdmin = false, // Even if false in DB
            IsReadOnly = false,
            CreatedUtc = DateTime.UtcNow.AddDays(-30),
            LastLoginUtc = DateTime.UtcNow
        };

        await DataContext.Users.AddAsync(user);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetMeQueryHandler>();
        var query = new GetMeQuery { UserId = adminId };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsAdmin.Should().BeTrue(); // Override to true
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMarkAsAdmin_WhenUserHasAdminFlag()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Username = "test_user_31",
            Id = userId,
            FirebaseUid = "firebase-admin2",
            Email = "admin2@example.com",
            DisplayName = "Admin User 2",
            SignInProvider = "password",
            EmailVerified = true,
            IsAdmin = true,
            IsReadOnly = false,
            CreatedUtc = DateTime.UtcNow.AddDays(-30),
            LastLoginUtc = DateTime.UtcNow
        };

        await DataContext.Users.AddAsync(user);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetMeQueryHandler>();
        var query = new GetMeQuery { UserId = userId };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsAdmin.Should().BeTrue();
    }
}



