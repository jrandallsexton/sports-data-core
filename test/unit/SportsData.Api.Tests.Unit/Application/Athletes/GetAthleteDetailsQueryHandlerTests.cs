using FluentAssertions;

using Moq;

using SportsData.Api.Application.Athletes.Queries.GetAthleteDetails;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Athlete;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Athletes;

public class GetAthleteDetailsQueryHandlerTests : UnitTestBase<GetAthleteDetailsQueryHandler>
{
    private readonly Mock<IAthleteClientFactory> _athleteClientFactoryMock;
    private readonly Mock<IProvideAthletes> _athleteClientMock;

    public GetAthleteDetailsQueryHandlerTests()
    {
        _athleteClientFactoryMock = Mocker.GetMock<IAthleteClientFactory>();
        _athleteClientMock = new Mock<IProvideAthletes>();
        _athleteClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_athleteClientMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_RelaysTheClientResult()
    {
        var athleteId = Guid.NewGuid();
        var dto = new AthleteDetailDto { Id = athleteId, DisplayName = "Arch Manning" };

        _athleteClientMock
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

        _athleteClientMock
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
        _athleteClientMock.Verify(
            x => x.GetAthleteDetails(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyAthleteId_Returns400Validation()
    {
        var handler = Mocker.CreateInstance<GetAthleteDetailsQueryHandler>();

        // The :guid route constraint admits Guid.Empty; it can never identify
        // a record, so it is malformed input, not a miss.
        var result = await handler.ExecuteAsync(
            new GetAthleteDetailsQuery("football", "ncaa", Guid.Empty));

        result.Status.Should().Be(ResultStatus.Validation);
        _athleteClientMock.Verify(
            x => x.GetAthleteDetails(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CallerCancels_PropagatesCancellation()
    {
        var athleteId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        _athleteClientMock
            .Setup(x => x.GetAthleteDetails(athleteId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var handler = Mocker.CreateInstance<GetAthleteDetailsQueryHandler>();
        await cts.CancelAsync();

        // A cancelled request must not be reported as a server error.
        var act = async () => await handler.ExecuteAsync(
            new GetAthleteDetailsQuery("football", "ncaa", athleteId), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_ClientThrows_ReturnsErrorNotException()
    {
        var athleteId = Guid.NewGuid();

        _athleteClientMock
            .Setup(x => x.GetAthleteDetails(athleteId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("producer unreachable"));

        var handler = Mocker.CreateInstance<GetAthleteDetailsQueryHandler>();

        var result = await handler.ExecuteAsync(
            new GetAthleteDetailsQuery("football", "ncaa", athleteId));

        result.Status.Should().Be(ResultStatus.Error);
    }
}
