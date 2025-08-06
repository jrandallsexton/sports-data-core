using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.LeagueCreationPage;

public class CreateLeagueCommandHandlerTests : ApiTestBase<CreateLeagueCommandHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldPersistPickemGroupAndReturnId()
    {
        // Arrange
        var handler = Mocker.CreateInstance<CreateLeagueCommandHandler>();

        var command = new CreateLeagueCommand
        {
            Name = "Bit's League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            UseConfidencePoints = false,
            IsPublic = true,
            CommissionerUserId = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
            RankingFilter = TeamRankingFilter.None
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        var entity = await base.DataContext.PickemGroups
            .FirstOrDefaultAsync(p => p.Id == result);

        entity.Should().NotBeNull();
        entity!.Name.Should().Be(command.Name);
        entity.Sport.Should().Be(command.Sport);
        entity.League.Should().Be(command.League);
        entity.PickType.Should().Be(command.PickType);
        entity.UseConfidencePoints.Should().Be(command.UseConfidencePoints);
        entity.TiebreakerType.Should().Be(command.TiebreakerType);
        entity.TiebreakerTiePolicy.Should().Be(command.TiebreakerTiePolicy);
        entity.IsPublic.Should().Be(command.IsPublic);
        entity.CommissionerUserId.Should().Be(command.CommissionerUserId);
        entity.CreatedBy.Should().Be(command.CommissionerUserId);
    }
}