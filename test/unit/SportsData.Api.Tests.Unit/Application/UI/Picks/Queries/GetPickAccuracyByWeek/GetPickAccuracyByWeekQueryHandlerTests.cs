using FluentAssertions;

using SportsData.Api.Application;
using SportsData.Api.Application.UI.Picks.Queries.GetPickAccuracyByWeek;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Picks.Queries.GetPickAccuracyByWeek;

public class GetPickAccuracyByWeekQueryHandlerTests : ApiTestBase<GetPickAccuracyByWeekQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenUserHasNoGroups()
    {
        // Arrange
        var userId = Guid.NewGuid();
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
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetPickAccuracyByWeekQueryHandler>();
        var query = new GetPickAccuracyByWeekQuery
        {
            UserId = userId,
            ForSynthetic = false
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCalculateWeeklyAccuracy_WhenPicksExist()
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

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };
        await DataContext.PickemGroups.AddAsync(group);

        var member = new PickemGroupMember
        {
            PickemGroupId = groupId,
            UserId = userId,
            Role = LeagueRole.Member
        };
        await DataContext.PickemGroupMembers.AddAsync(member);

        // Week 5: 2 correct, 1 incorrect
        for (var i = 0; i < 3; i++)
        {
            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PickemGroupId = groupId,
                ContestId = Guid.NewGuid(),
                Week = 5,
                PickType = PickType.StraightUp,
                IsCorrect = i < 2,
                PointsAwarded = i < 2 ? 1 : 0,
                TiebreakerType = TiebreakerType.TotalPoints
            };
            await DataContext.UserPicks.AddAsync(pick);
        }

        // Week 6: 1 correct, 1 incorrect
        for (var i = 0; i < 2; i++)
        {
            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PickemGroupId = groupId,
                ContestId = Guid.NewGuid(),
                Week = 6,
                PickType = PickType.StraightUp,
                IsCorrect = i < 1,
                PointsAwarded = i < 1 ? 1 : 0,
                TiebreakerType = TiebreakerType.TotalPoints
            };
            await DataContext.UserPicks.AddAsync(pick);
        }

        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetPickAccuracyByWeekQueryHandler>();
        var query = new GetPickAccuracyByWeekQuery
        {
            UserId = userId,
            ForSynthetic = false
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        var dto = result.Value[0];
        dto.LeagueName.Should().Be("Test League");
        dto.WeeklyAccuracy.Should().HaveCount(2);

        var week5 = dto.WeeklyAccuracy.First(w => w.Week == 5);
        week5.CorrectPicks.Should().Be(2);
        week5.TotalPicks.Should().Be(3);
        week5.AccuracyPercent.Should().BeApproximately(66.7, 0.1);

        var week6 = dto.WeeklyAccuracy.First(w => w.Week == 6);
        week6.CorrectPicks.Should().Be(1);
        week6.TotalPicks.Should().Be(2);
        week6.AccuracyPercent.Should().Be(50.0);

        // Overall: 3 correct out of 5 = 60%
        dto.OverallAccuracyPercent.Should().Be(60.0);
    }

    [Fact]
    public async Task ExecuteForSyntheticAsync_ShouldReturnNotFound_WhenNoSyntheticUser()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetPickAccuracyByWeekQueryHandler>();
        var query = new GetPickAccuracyByWeekQuery
        {
            UserId = Guid.NewGuid(),
            ForSynthetic = true
        };

        // Act
        var result = await handler.ExecuteForSyntheticAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteForSyntheticAsync_ShouldReturnAllGroupsData_WhenSyntheticUserExists()
    {
        // Arrange
        var syntheticUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var syntheticUser = new UserEntity
        {
            Id = syntheticUserId,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "synthetic@test.com",
            DisplayName = "AI Bot",
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow,
            IsSynthetic = true
        };
        await DataContext.Users.AddAsync(syntheticUser);

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };
        await DataContext.PickemGroups.AddAsync(group);

        // Add some picks with PointsAwarded (scored picks)
        for (var i = 0; i < 4; i++)
        {
            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                UserId = syntheticUserId,
                PickemGroupId = groupId,
                ContestId = Guid.NewGuid(),
                Week = 5,
                PickType = PickType.StraightUp,
                IsCorrect = i < 3,
                PointsAwarded = i < 3 ? 1 : 0,
                TiebreakerType = TiebreakerType.TotalPoints
            };
            await DataContext.UserPicks.AddAsync(pick);
        }

        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetPickAccuracyByWeekQueryHandler>();
        var query = new GetPickAccuracyByWeekQuery
        {
            UserId = Guid.NewGuid(), // Different user ID - should still find synthetic
            ForSynthetic = true
        };

        // Act
        var result = await handler.ExecuteForSyntheticAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(syntheticUserId);
        result.Value.UserName.Should().Be("AI Bot");
        result.Value.LeagueName.Should().Be("All Groups");
        result.Value.WeeklyAccuracy.Should().HaveCount(1);
        result.Value.WeeklyAccuracy[0].Week.Should().Be(5);
        result.Value.WeeklyAccuracy[0].CorrectPicks.Should().Be(3);
        result.Value.WeeklyAccuracy[0].TotalPicks.Should().Be(4);
    }

    [Fact]
    public async Task ExecuteForSyntheticAsync_ShouldCalculateOverallAccuracyCorrectly()
    {
        // Arrange
        var syntheticUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var syntheticUser = new UserEntity
        {
            Id = syntheticUserId,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "synthetic@test.com",
            DisplayName = "AI Bot",
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow,
            IsSynthetic = true
        };
        await DataContext.Users.AddAsync(syntheticUser);

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };
        await DataContext.PickemGroups.AddAsync(group);

        // Week 5: 3 correct out of 4 (75%)
        for (var i = 0; i < 4; i++)
        {
            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                UserId = syntheticUserId,
                PickemGroupId = groupId,
                ContestId = Guid.NewGuid(),
                Week = 5,
                PickType = PickType.StraightUp,
                IsCorrect = i < 3,
                PointsAwarded = i < 3 ? 1 : 0,
                TiebreakerType = TiebreakerType.TotalPoints
            };
            await DataContext.UserPicks.AddAsync(pick);
        }

        // Week 6: 1 correct out of 2 (50%)
        for (var i = 0; i < 2; i++)
        {
            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                UserId = syntheticUserId,
                PickemGroupId = groupId,
                ContestId = Guid.NewGuid(),
                Week = 6,
                PickType = PickType.StraightUp,
                IsCorrect = i < 1,
                PointsAwarded = i < 1 ? 1 : 0,
                TiebreakerType = TiebreakerType.TotalPoints
            };
            await DataContext.UserPicks.AddAsync(pick);
        }

        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetPickAccuracyByWeekQueryHandler>();
        var query = new GetPickAccuracyByWeekQuery
        {
            UserId = Guid.NewGuid(),
            ForSynthetic = true
        };

        // Act
        var result = await handler.ExecuteForSyntheticAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WeeklyAccuracy.Should().HaveCount(2);
        
        // Overall: 4 correct out of 6 total = 66.7%
        result.Value.OverallAccuracyPercent.Should().BeApproximately(66.7, 0.1);
    }

    [Fact]
    public async Task ExecuteForSyntheticAsync_ShouldReturnZeroPercent_WhenNoScoredPicks()
    {
        // Arrange
        var syntheticUserId = Guid.NewGuid();

        var syntheticUser = new UserEntity
        {
            Id = syntheticUserId,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "synthetic@test.com",
            DisplayName = "AI Bot",
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow,
            IsSynthetic = true
        };
        await DataContext.Users.AddAsync(syntheticUser);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetPickAccuracyByWeekQueryHandler>();
        var query = new GetPickAccuracyByWeekQuery
        {
            UserId = Guid.NewGuid(),
            ForSynthetic = true
        };

        // Act
        var result = await handler.ExecuteForSyntheticAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WeeklyAccuracy.Should().BeEmpty();
        result.Value.OverallAccuracyPercent.Should().Be(0);
    }
}
