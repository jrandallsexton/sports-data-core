using FluentAssertions;

using SportsData.Api.Application;
using SportsData.Api.Application.UI.Picks.Queries.GetPickRecordWidget;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Picks.Queries.GetPickRecordWidget;

public class GetPickRecordWidgetQueryHandlerTests : ApiTestBase<GetPickRecordWidgetQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyWidget_WhenUserHasNoGroups()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetPickRecordWidgetQueryHandler>();
        var query = new GetPickRecordWidgetQuery
        {
            UserId = Guid.NewGuid(),
            SeasonYear = 2025,
            ForSynthetic = false
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SeasonYear.Should().Be(2025);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenForSyntheticAndNoSyntheticUserExists()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetPickRecordWidgetQueryHandler>();
        var query = new GetPickRecordWidgetQuery
        {
            UserId = Guid.NewGuid(),
            SeasonYear = 2025,
            ForSynthetic = true
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCalculateAccuracy_WhenUserHasPicks()
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

        // 3 correct, 2 incorrect
        for (var i = 0; i < 5; i++)
        {
            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                UserId = userId,
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

        var handler = Mocker.CreateInstance<GetPickRecordWidgetQueryHandler>();
        var query = new GetPickRecordWidgetQuery
        {
            UserId = userId,
            SeasonYear = 2025,
            ForSynthetic = false
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].LeagueName.Should().Be("Test League");
        result.Value.Items[0].Correct.Should().Be(3);
        result.Value.Items[0].Incorrect.Should().Be(2);
        result.Value.Items[0].Accuracy.Should().Be(0.6);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnItemsOrderedByLeagueName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId1 = Guid.NewGuid();
        var groupId2 = Guid.NewGuid();

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

        var group1 = new PickemGroup
        {
            Id = groupId1,
            Name = "Zebra League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };
        var group2 = new PickemGroup
        {
            Id = groupId2,
            Name = "Alpha League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };
        await DataContext.PickemGroups.AddRangeAsync(group1, group2);

        var member1 = new PickemGroupMember { PickemGroupId = groupId1, UserId = userId, Role = LeagueRole.Member };
        var member2 = new PickemGroupMember { PickemGroupId = groupId2, UserId = userId, Role = LeagueRole.Member };
        await DataContext.PickemGroupMembers.AddRangeAsync(member1, member2);

        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetPickRecordWidgetQueryHandler>();
        var query = new GetPickRecordWidgetQuery
        {
            UserId = userId,
            SeasonYear = 2025,
            ForSynthetic = false
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].LeagueName.Should().Be("Alpha League");
        result.Value.Items[1].LeagueName.Should().Be("Zebra League");
    }
}
