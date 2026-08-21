using FluentAssertions;

using Moq;

using SportsData.Api.Application.Athletes.Queries.GetAthleteDetails;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Athletes;

public class GetAthleteDetailsQueryHandlerTests : UnitTestBase<GetAthleteDetailsQueryHandler>
{
    private readonly Mock<IFranchiseClientFactory> _franchiseClientFactoryMock;
    private readonly Mock<IProvideFranchises> _franchiseClientMock;

    public GetAthleteDetailsQueryHandlerTests()
    {
        _franchiseClientFactoryMock = Mocker.GetMock<IFranchiseClientFactory>();
        _franchiseClientMock = new Mock<IProvideFranchises>();
        _franchiseClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_franchiseClientMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_RelaysTheClientResult()
    {
        var athleteId = Guid.NewGuid();
        var dto = new AthleteDetailDto { Id = athleteId, DisplayName = "Arch Manning" };

        _franchiseClientMock
            .Setup(x => x.GetAthleteDetails(athleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<AthleteDetailDto>(dto));

        var handler = Mocker.CreateInstance<GetAthleteDetailsQueryHandler>();

        var result = await handler.ExecuteAsync(
            new GetAthleteDetailsQuery("football", "ncaa", athleteId));

        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("Arch Manning");
    }

    [Fact]
    public async Task ExecuteAsync_NotFoundFromClient_PassesThrough()
    {
        var athleteId = Guid.NewGuid();

        _franchiseClientMock
            .Setup(x => x.GetAthleteDetails(athleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<AthleteDetailDto>(default!, ResultStatus.NotFound, []));

        var handler = Mocker.CreateInstance<GetAthleteDetailsQueryHandler>();

        var result = await handler.ExecuteAsync(
            new GetAthleteDetailsQuery("football", "ncaa", athleteId));

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedSportLeague_Returns400Validation()
    {
        var handler = Mocker.CreateInstance<GetAthleteDetailsQueryHandler>();

        var result = await handler.ExecuteAsync(
            new GetAthleteDetailsQuery("cricket", "ipl", Guid.NewGuid()));

        result.Status.Should().Be(ResultStatus.Validation);
        _franchiseClientMock.Verify(
            x => x.GetAthleteDetails(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ClientThrows_ReturnsErrorNotException()
    {
        var athleteId = Guid.NewGuid();

        _franchiseClientMock
            .Setup(x => x.GetAthleteDetails(athleteId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("producer unreachable"));

        var handler = Mocker.CreateInstance<GetAthleteDetailsQueryHandler>();

        var result = await handler.ExecuteAsync(
            new GetAthleteDetailsQuery("football", "ncaa", athleteId));

        result.Status.Should().Be(ResultStatus.Error);
    }
}
