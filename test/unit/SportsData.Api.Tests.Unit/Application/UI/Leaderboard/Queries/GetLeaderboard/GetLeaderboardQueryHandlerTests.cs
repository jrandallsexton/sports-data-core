using FluentAssertions;

using SportsData.Api.Application;
using SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboard;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leaderboard.Queries.GetLeaderboard;

public class GetLeaderboardQueryHandlerTests : ApiTestBase<GetLeaderboardQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenGroupIdIsEmpty()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetLeaderboardQueryHandler>();
        var query = new GetLeaderboardQuery { GroupId = Guid.Empty };

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
        var query = new GetLeaderboardQuery { GroupId = groupId };

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
        var query = new GetLeaderboardQuery { GroupId = groupId };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
