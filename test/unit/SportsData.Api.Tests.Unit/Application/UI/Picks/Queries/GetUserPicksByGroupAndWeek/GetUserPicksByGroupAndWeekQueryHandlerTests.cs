using FluentAssertions;

using SportsData.Api.Application;
using SportsData.Api.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;

public class GetUserPicksByGroupAndWeekQueryHandlerTests : ApiTestBase<GetUserPicksByGroupAndWeekQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenNoPicksExist()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetUserPicksByGroupAndWeekQueryHandler>();
        var query = new GetUserPicksByGroupAndWeekQuery
        {
            UserId = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            WeekNumber = 1
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUserPicks_WhenPicksExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        var user = new UserEntity
        {
            Id = userId,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "test@test.com",
            DisplayName = "Test User",
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow
        };
        await DataContext.Users.AddAsync(user);

        var pick = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = groupId,
            ContestId = contestId,
            Week = 5,
            PickType = PickType.StraightUp,
            FranchiseId = Guid.NewGuid(),
            ConfidencePoints = 7,
            IsCorrect = true,
            PointsAwarded = 7,
            TiebreakerType = TiebreakerType.TotalPoints
        };
        await DataContext.UserPicks.AddAsync(pick);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetUserPicksByGroupAndWeekQueryHandler>();
        var query = new GetUserPicksByGroupAndWeekQuery
        {
            UserId = userId,
            GroupId = groupId,
            WeekNumber = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].UserId.Should().Be(userId);
        result.Value[0].ContestId.Should().Be(contestId);
        result.Value[0].PickType.Should().Be(PickType.StraightUp);
        result.Value[0].ConfidencePoints.Should().Be(7);
        result.Value[0].IsCorrect.Should().BeTrue();
        result.Value[0].PointsAwarded.Should().Be(7);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFilterByWeek_WhenMultipleWeeksExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var user = new UserEntity
        {
            Id = userId,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "test@test.com",
            DisplayName = "Test User",
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow
        };
        await DataContext.Users.AddAsync(user);

        var pick1 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = groupId,
            ContestId = Guid.NewGuid(),
            Week = 5,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints
        };
        var pick2 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = groupId,
            ContestId = Guid.NewGuid(),
            Week = 6,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints
        };
        await DataContext.UserPicks.AddRangeAsync(pick1, pick2);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetUserPicksByGroupAndWeekQueryHandler>();
        var query = new GetUserPicksByGroupAndWeekQuery
        {
            UserId = userId,
            GroupId = groupId,
            WeekNumber = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].ContestId.Should().Be(pick1.ContestId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFilterByUser_WhenMultipleUsersExist()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var user1 = new UserEntity
        {
            Id = userId1,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "test1@test.com",
            DisplayName = "Test User 1",
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow
        };
        var user2 = new UserEntity
        {
            Id = userId2,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "test2@test.com",
            DisplayName = "Test User 2",
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow
        };
        await DataContext.Users.AddRangeAsync(user1, user2);

        var pick1 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId1,
            PickemGroupId = groupId,
            ContestId = Guid.NewGuid(),
            Week = 5,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints
        };
        var pick2 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId2,
            PickemGroupId = groupId,
            ContestId = Guid.NewGuid(),
            Week = 5,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints
        };
        await DataContext.UserPicks.AddRangeAsync(pick1, pick2);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetUserPicksByGroupAndWeekQueryHandler>();
        var query = new GetUserPicksByGroupAndWeekQuery
        {
            UserId = userId1,
            GroupId = groupId,
            WeekNumber = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].UserId.Should().Be(userId1);
    }
}
