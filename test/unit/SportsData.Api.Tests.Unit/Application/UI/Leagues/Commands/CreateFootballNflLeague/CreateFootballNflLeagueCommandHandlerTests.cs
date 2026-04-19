using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague.Dtos;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise;

using Xunit;

using FailureResult = SportsData.Core.Common.Failure<System.Guid>;

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

        // Use the real validator so Name/enum/date-window assertions exercise the
        // production rules rather than AutoMocker's default IsValid=true stub.
        Mocker.Use<FluentValidation.IValidator<CreateFootballNflLeagueRequest>>(
            new CreateFootballNflLeagueRequestValidator());
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

        var failure = result.Should().BeOfType<FailureResult>().Subject;
        failure.Status.Should().Be(ResultStatus.Validation);
        failure.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(CreateFootballNflLeagueRequest.Name));
        failure.Errors.Single().ErrorMessage.Should().Contain("League name is required");
    }

    [Fact]
    public async Task ShouldFail_WhenPickTypeIsInvalid()
    {
        var sut = Mocker.CreateInstance<CreateFootballNflLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.PickType = "Garbage";

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        var failure = result.Should().BeOfType<FailureResult>().Subject;
        failure.Status.Should().Be(ResultStatus.Validation);
        failure.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(CreateFootballNflLeagueRequest.PickType));
        failure.Errors.Single().ErrorMessage.Should().Contain("Invalid pick type: Garbage");
    }

    [Fact]
    public async Task ShouldFail_WhenAnyDivisionSlugNotResolved()
    {
        // Pin SeasonYear on the request so the handler's DateTime.UtcNow.Year
        // fallback never fires — the mock setup below stays deterministic.
        const int SeasonYear = 2025;

        var request = BuildValidRequest();
        request.SeasonYear = SeasonYear;
        var currentUserId = Guid.NewGuid();

        var slugToGuid = new Dictionary<Guid, string>
        {
            [Guid.NewGuid()] = "afc-east",
            [Guid.NewGuid()] = "afc-north"
        };

        request.DivisionSlugs = ["afc-east", "afc-north", "garbage"];

        _franchiseClientMock
            .Setup(x => x.GetConferenceIdsBySlugs(SeasonYear, request.DivisionSlugs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slugToGuid);

        var sut = Mocker.CreateInstance<CreateFootballNflLeagueCommandHandler>();

        var result = await sut.ExecuteAsync(request, currentUserId);

        var failure = result.Should().BeOfType<FailureResult>().Subject;
        failure.Status.Should().Be(ResultStatus.Validation);
        failure.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(CreateFootballNflLeagueRequest.DivisionSlugs));
        failure.Errors.Single().ErrorMessage.Should().Contain("Unknown division slugs")
            .And.Contain("garbage");
    }
}
