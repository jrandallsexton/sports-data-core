using FluentAssertions;
using SportsData.Api.Application.Common.Enums;

using Moq;

using SportsData.Api.Application.UI.Leagues.Commands.CreateLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateLeague.Dtos;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands.CreateLeague;

public class CreateLeagueCommandHandlerTests : ApiTestBase<CreateLeagueCommandHandler>
{
    private readonly Mock<IFranchiseClientFactory> _franchiseClientFactoryMock;
    private readonly Mock<IProvideFranchises> _franchiseClientMock;

    public CreateLeagueCommandHandlerTests()
    {
        _franchiseClientFactoryMock = Mocker.GetMock<IFranchiseClientFactory>();
        _franchiseClientMock = new Mock<IProvideFranchises>();
        _franchiseClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_franchiseClientMock.Object);
    }

    private CreateLeagueRequest BuildValidRequest() => new()
    {
        Name = "My League",
        PickType = "StraightUp",
        TiebreakerType = "TotalPoints",
        TiebreakerTiePolicy = "EarliestSubmission",
        ConferenceSlugs = new List<string> { "sec" },
        UseConfidencePoints = false,
        IsPublic = true
    };

    [Fact]
    public async Task ShouldFail_WhenNameIsNull()
    {
        var sut = Mocker.CreateInstance<CreateLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.Name = null!;

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldFail_WhenPickTypeIsInvalid()
    {
        var sut = Mocker.CreateInstance<CreateLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.PickType = "Garbage";

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldFail_WhenTiebreakerTypeIsInvalid()
    {
        var sut = Mocker.CreateInstance<CreateLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.TiebreakerType = "Garbage";

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldFail_WhenTiebreakerTiePolicyIsInvalid()
    {
        var sut = Mocker.CreateInstance<CreateLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.RankingFilter = "AP_TOP_25";
        request.TiebreakerTiePolicy = "Nope";

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldFail_WhenAnyConferenceSlugNotResolved()
    {
        var request = BuildValidRequest();
        var currentUserId = Guid.NewGuid();
        var currentYear = DateTime.UtcNow.Year;

        var slugToGuid = new Dictionary<Guid, string>
        {
            [Guid.NewGuid()] = "acc",
            [Guid.NewGuid()] = "big12"
        };

        request.RankingFilter = "AP_TOP_25";
        request.ConferenceSlugs = ["acc", "big12", "garbage"];

        _franchiseClientMock
            .Setup(x => x.GetConferenceIdsBySlugs(currentYear, request.ConferenceSlugs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slugToGuid);

        var sut = Mocker.CreateInstance<CreateLeagueCommandHandler>();

        var result = await sut.ExecuteAsync(request, currentUserId);

        result.IsSuccess.Should().BeFalse();
    }
}
