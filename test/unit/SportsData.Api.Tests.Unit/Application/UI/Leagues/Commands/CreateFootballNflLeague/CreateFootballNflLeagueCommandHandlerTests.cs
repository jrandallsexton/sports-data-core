using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague.Dtos;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands.CreateFootballNflLeague;

public class CreateFootballNflLeagueCommandHandlerTests : ApiTestBase<CreateFootballNflLeagueCommandHandler>
{
    private readonly Mock<IFranchiseClientFactory> _franchiseClientFactoryMock;
    private readonly Mock<IProvideFranchises> _franchiseClientMock;

    public CreateFootballNflLeagueCommandHandlerTests()
    {
        _franchiseClientFactoryMock = Mocker.GetMock<IFranchiseClientFactory>();
        _franchiseClientMock = new Mock<IProvideFranchises>();
        _franchiseClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_franchiseClientMock.Object);
    }

    private CreateFootballNflLeagueRequest BuildValidRequest() => new()
    {
        Name = "My NFL League",
        PickType = "StraightUp",
        TiebreakerType = "TotalPoints",
        TiebreakerTiePolicy = "EarliestSubmission",
        DivisionSlugs = new List<string> { "afc-east" },
        UseConfidencePoints = false,
        IsPublic = true
    };

    [Fact]
    public async Task ShouldFail_WhenNameIsNull()
    {
        var sut = Mocker.CreateInstance<CreateFootballNflLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.Name = null!;

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldFail_WhenPickTypeIsInvalid()
    {
        var sut = Mocker.CreateInstance<CreateFootballNflLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.PickType = "Garbage";

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldFail_WhenAnyDivisionSlugNotResolved()
    {
        var request = BuildValidRequest();
        var currentUserId = Guid.NewGuid();
        var currentYear = DateTime.UtcNow.Year;

        var slugToGuid = new Dictionary<Guid, string>
        {
            [Guid.NewGuid()] = "afc-east",
            [Guid.NewGuid()] = "afc-north"
        };

        request.DivisionSlugs = ["afc-east", "afc-north", "garbage"];

        _franchiseClientMock
            .Setup(x => x.GetConferenceIdsBySlugs(currentYear, request.DivisionSlugs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slugToGuid);

        var sut = Mocker.CreateInstance<CreateFootballNflLeagueCommandHandler>();

        var result = await sut.ExecuteAsync(request, currentUserId);

        result.IsSuccess.Should().BeFalse();
    }
}
