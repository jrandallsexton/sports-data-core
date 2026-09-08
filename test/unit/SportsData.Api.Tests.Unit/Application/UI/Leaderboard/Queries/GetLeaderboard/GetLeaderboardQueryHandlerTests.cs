using FluentAssertions;
using SportsData.Api.Application.Common.Enums;

using SportsData.Api.Application;
using SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboard;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leaderboard.Queries.GetLeaderboard;

public class GetLeaderboardQueryHandlerTests : ApiTestBase<GetLeaderboardQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenGroupIdIsEmpty()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetLeaderboardQueryHandler>();
        var query = new GetLeaderboardQuery { GroupId = Guid.Empty ,
            UserId = Guid.NewGuid()};

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenGroupDoesNotExist()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var sut = Mocker.CreateInstance<GetLeaderboardQueryHandler>();
        var query = new GetLeaderboardQuery { GroupId = groupId ,
            UserId = Guid.NewGuid()};

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenGroupExistsButHasNoPicks()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Test League",
            CreatedBy = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            CommissionerUserId = Guid.NewGuid(),
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<GetLeaderboardQueryHandler>();
        var query = new GetLeaderboardQuery { GroupId = groupId ,
            UserId = Guid.NewGuid()};

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PushExcludedFromAccuracyDenominator_PendingNeverCounted()
    {
        // A PUSH (ScoredAt set, IsCorrect null, 0 points) is excluded from
        // TotalPicks — the bet never happened (2026-09-07 SMU@FSU). A pending
        // pick (ScoredAt null) never reaches the grouping at all.
        var groupId = Guid.NewGuid();
        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Push League",
            CreatedBy = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            CommissionerUserId = Guid.NewGuid(),
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };
        await DataContext.PickemGroups.AddAsync(group);

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Username = "push_user",
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "push@test.com",
            DisplayName = "Push User",
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow
        };
        await DataContext.Users.AddAsync(user);

        var scoredAt = DateTime.UtcNow;
        // One decided-correct pick, one push, one pending.
        await DataContext.UserPicks.AddRangeAsync(
            new PickemGroupUserPick
            {
                Id = Guid.NewGuid(), UserId = user.Id, PickemGroupId = groupId,
                ContestId = Guid.NewGuid(), Week = 1, PickType = PickType.AgainstTheSpread,
                IsCorrect = true, PointsAwarded = 1, ScoredAt = scoredAt,
                TiebreakerType = TiebreakerType.TotalPoints
            },
            new PickemGroupUserPick
            {
                Id = Guid.NewGuid(), UserId = user.Id, PickemGroupId = groupId,
                ContestId = Guid.NewGuid(), Week = 1, PickType = PickType.AgainstTheSpread,
                IsCorrect = null, PointsAwarded = 0, ScoredAt = scoredAt, // PUSH
                TiebreakerType = TiebreakerType.TotalPoints
            },
            new PickemGroupUserPick
            {
                Id = Guid.NewGuid(), UserId = user.Id, PickemGroupId = groupId,
                ContestId = Guid.NewGuid(), Week = 1, PickType = PickType.AgainstTheSpread,
                IsCorrect = null, PointsAwarded = null, ScoredAt = null, // pending
                TiebreakerType = TiebreakerType.TotalPoints
            });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<GetLeaderboardQueryHandler>();
        var result = await sut.ExecuteAsync(new GetLeaderboardQuery
        {
            GroupId = groupId,
            UserId = user.Id
        });

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Should().ContainSingle().Subject;
        row.TotalPicks.Should().Be(1, "the push and the pending pick are not decided picks");
        row.TotalCorrect.Should().Be(1);
        row.PickAccuracy.Should().Be(100m);
        row.TotalPoints.Should().Be(1, "a push awards nothing");
    }

}