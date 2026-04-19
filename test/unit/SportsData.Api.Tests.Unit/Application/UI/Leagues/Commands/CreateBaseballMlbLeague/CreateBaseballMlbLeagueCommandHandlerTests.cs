using FluentAssertions;

using FluentValidation;

using Moq;

using SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague.Dtos;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands.CreateBaseballMlbLeague;

public class CreateBaseballMlbLeagueCommandHandlerTests : ApiTestBase<CreateBaseballMlbLeagueCommandHandler>
{
    private readonly Mock<IFranchiseClientFactory> _franchiseClientFactoryMock;
    private readonly Mock<IProvideFranchises> _franchiseClientMock;

    public CreateBaseballMlbLeagueCommandHandlerTests()
    {
        _franchiseClientFactoryMock = Mocker.GetMock<IFranchiseClientFactory>();
        _franchiseClientMock = new Mock<IProvideFranchises>();
        _franchiseClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_franchiseClientMock.Object);

        // Use the real validator so Name/enum/date-window assertions exercise the
        // production rules rather than AutoMocker's default IsValid=true stub.
        Mocker.Use<IValidator<CreateBaseballMlbLeagueRequest>>(
            new CreateBaseballMlbLeagueRequestValidator());
    }

    private CreateBaseballMlbLeagueRequest BuildValidRequest() => new()
    {
        Name = "My MLB League",
        PickType = "StraightUp",
        TiebreakerType = "TotalPoints",
        TiebreakerTiePolicy = "EarliestSubmission",
        DivisionSlugs = new List<string> { "american-league-east" },
        UseConfidencePoints = false,
        IsPublic = true
    };

    [Fact]
    public async Task ShouldFail_WhenNameIsNull()
    {
        var sut = Mocker.CreateInstance<CreateBaseballMlbLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.Name = null!;

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldFail_WhenPickTypeIsInvalid()
    {
        var sut = Mocker.CreateInstance<CreateBaseballMlbLeagueCommandHandler>();
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
            [Guid.NewGuid()] = "american-league-east",
            [Guid.NewGuid()] = "american-league-central"
        };

        request.DivisionSlugs = ["american-league-east", "american-league-central", "garbage"];

        _franchiseClientMock
            .Setup(x => x.GetConferenceIdsBySlugs(currentYear, request.DivisionSlugs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slugToGuid);

        var sut = Mocker.CreateInstance<CreateBaseballMlbLeagueCommandHandler>();

        var result = await sut.ExecuteAsync(request, currentUserId);

        result.IsSuccess.Should().BeFalse();
    }
}
