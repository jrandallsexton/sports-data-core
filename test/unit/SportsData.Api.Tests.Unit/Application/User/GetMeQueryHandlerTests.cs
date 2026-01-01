using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application;
using SportsData.Api.Application.User.Dtos;
using SportsData.Api.Application.User.Queries.GetMe;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.User.Queries.GetMe;

public class GetMeQueryHandlerTests : ApiTestBase<GetMeQueryHandler>
{
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
        var user = new Infrastructure.Data.Entities.User
        {
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

        var user = new Infrastructure.Data.Entities.User
        {
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
        result.Value.Leagues.First().MaxSeasonWeek.Should().Be(5);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCalculateMaxSeasonWeek_WhenMultipleWeeksExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var user = new Infrastructure.Data.Entities.User
        {
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
        user.GroupMemberships.Add(membership);

        await DataContext.Users.AddAsync(user);
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupWeeks.AddRangeAsync([week1, week2, week3]);
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
        result.Value.Leagues.First().MaxSeasonWeek.Should().Be(7);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMarkAsAdmin_WhenUserIsHardcodedAdmin()
    {
        // Arrange
        var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var user = new Infrastructure.Data.Entities.User
        {
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
        var user = new Infrastructure.Data.Entities.User
        {
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
