using FluentAssertions;

using Moq;

using SportsData.Api.Application;
using SportsData.Api.Application.UI.Picks;
using SportsData.Api.Application.UI.Picks.PicksPage;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Picks;

public class PickServiceTests : ApiTestBase<PickService>
{
    [Fact]
    public async Task SubmitPickAsync_ShouldDispatchCommand_WhenValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            StartDateUtc = DateTime.UtcNow.AddHours(1)
        };

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Soft Launch League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };

        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.SaveChangesAsync();

        var handlerMock = Mocker.GetMock<ISubmitUserPickCommandHandler>();

        var service = Mocker.CreateInstance<PickService>();

        var request = new SubmitUserPickRequest
        {
            PickemGroupId = groupId,
            ContestId = contestId,
            PickType = PickType.StraightUp,
            FranchiseSeasonId = Guid.NewGuid(),
            OverUnder = OverUnderPick.Over,
            ConfidencePoints = 5,
            TiebreakerGuessTotal = 49,
            TiebreakerGuessHome = 27,
            TiebreakerGuessAway = 22
        };

        // Act
        await service.SubmitPickAsync(userId, request, CancellationToken.None);

        // Assert
        handlerMock.Verify(h => h.Handle(
            It.Is<SubmitUserPickCommand>(cmd =>
                cmd.UserId == userId &&
                cmd.PickemGroupId == groupId &&
                cmd.ContestId == contestId &&
                cmd.PickType == PickType.StraightUp &&
                cmd.FranchiseSeasonId == request.FranchiseSeasonId &&
                cmd.OverUnder == request.OverUnder &&
                cmd.ConfidencePoints == request.ConfidencePoints &&
                cmd.TiebreakerGuessTotal == request.TiebreakerGuessTotal &&
                cmd.TiebreakerGuessHome == request.TiebreakerGuessHome &&
                cmd.TiebreakerGuessAway == request.TiebreakerGuessAway
            ), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task SubmitPickAsync_ShouldThrow_WhenMatchupIsLocked()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            StartDateUtc = DateTime.UtcNow.AddMinutes(2) // Within lock window
        };

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Soft Launch League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };

        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.SaveChangesAsync();

        var service = Mocker.CreateInstance<PickService>();

        var request = new SubmitUserPickRequest
        {
            PickemGroupId = groupId,
            ContestId = contestId,
            PickType = PickType.StraightUp
        };

        // Act
        var result = await service.SubmitPickAsync(userId, request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }
}
