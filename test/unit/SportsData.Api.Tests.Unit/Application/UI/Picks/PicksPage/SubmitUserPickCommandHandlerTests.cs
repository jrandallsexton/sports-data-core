using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application;
using SportsData.Api.Application.UI.Picks.PicksPage;
using SportsData.Api.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Picks.PicksPage;

public class SubmitUserPickCommandHandlerTests : ApiTestBase<SubmitUserPickCommandHandler>
{
    [Fact]
    public async Task Handle_ShouldCreateNewPick_WhenNoneExists()
    {
        // Arrange
        var handler = Mocker.CreateInstance<SubmitUserPickCommandHandler>();

        var command = new SubmitUserPickCommand
        {
            UserId = Guid.NewGuid(),
            PickemGroupId = Guid.NewGuid(),
            ContestId = Guid.NewGuid(),
            PickType = UserPickType.StraightUp,
            FranchiseSeasonId = Guid.NewGuid(),
            OverUnder = OverUnderPick.Over,
            ConfidencePoints = 7,
            TiebreakerGuessTotal = 52,
            TiebreakerGuessHome = 28,
            TiebreakerGuessAway = 24
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var pick = await DataContext.UserPicks.FirstOrDefaultAsync();

        pick.Should().NotBeNull();
        pick!.UserId.Should().Be(command.UserId);
        pick.PickemGroupId.Should().Be(command.PickemGroupId);
        pick.ContestId.Should().Be(command.ContestId);
        pick.PickType.Should().Be(command.PickType);
        pick.FranchiseId.Should().Be(command.FranchiseSeasonId);
        pick.OverUnder.Should().Be(command.OverUnder);
        pick.ConfidencePoints.Should().Be(command.ConfidencePoints);
        pick.TiebreakerGuessTotal.Should().Be(command.TiebreakerGuessTotal);
        pick.TiebreakerGuessHome.Should().Be(command.TiebreakerGuessHome);
        pick.TiebreakerGuessAway.Should().Be(command.TiebreakerGuessAway);
    }

    [Fact]
    public async Task Handle_ShouldUpdateExistingPick_WhenOneExists()
    {
        // Arrange
        var existingPick = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PickemGroupId = Guid.NewGuid(),
            ContestId = Guid.NewGuid(),
            PickType = UserPickType.StraightUp,
            FranchiseId = Guid.NewGuid(),
            TiebreakerType = TiebreakerType.TotalPoints
        };

        await DataContext.UserPicks.AddAsync(existingPick);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<SubmitUserPickCommandHandler>();

        var command = new SubmitUserPickCommand
        {
            UserId = existingPick.UserId,
            PickemGroupId = existingPick.PickemGroupId,
            ContestId = existingPick.ContestId,
            PickType = existingPick.PickType,
            FranchiseSeasonId = Guid.NewGuid(),
            OverUnder = OverUnderPick.Under,
            ConfidencePoints = 3,
            TiebreakerGuessTotal = 45,
            TiebreakerGuessHome = 21,
            TiebreakerGuessAway = 24
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updated = await DataContext.UserPicks.FirstOrDefaultAsync();

        updated.Should().NotBeNull();
        updated!.FranchiseId.Should().Be(command.FranchiseSeasonId);
        updated.OverUnder.Should().Be(command.OverUnder);
        updated.ConfidencePoints.Should().Be(command.ConfidencePoints);
        updated.TiebreakerGuessTotal.Should().Be(command.TiebreakerGuessTotal);
        updated.TiebreakerGuessHome.Should().Be(command.TiebreakerGuessHome);
        updated.TiebreakerGuessAway.Should().Be(command.TiebreakerGuessAway);
    }
}
