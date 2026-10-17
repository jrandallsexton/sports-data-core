using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Producer.Application.Competitions;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions;

public class CompetitionControllerTests
{
    [Theory]
    [InlineData(Sport.BaseballMlb)]
    [InlineData(Sport.GolfPga)]
    public async Task GetCompetitionsWithoutDrives_OnANonFootballPod_IsBadRequest_NotADiResolutionError(Sport sport)
    {
        // The drives handler is registered on football pods only; the route
        // is mapped everywhere, so it must guard before resolving the handler.
        var appMode = new Mock<IAppMode>();
        appMode.Setup(x => x.CurrentSport).Returns(sport);

        var result = await new CompetitionController().GetCompetitionsWithoutDrives(null, appMode.Object, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}
