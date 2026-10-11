using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using Moq;

using SportsData.Api.Application.InstantAnalysisLab;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.InstantAnalysisLab;

/// <summary>
/// The lab exists only on football Producer pods, so any other sport is
/// rejected at the edge instead of failing as a 500 in Producer.
/// </summary>
public class InstantAnalysisLabControllerTests
{
    private readonly Mock<IContestClientFactory> _factory = new();
    private readonly Mock<IProvideContests> _client = new();
    private readonly InstantAnalysisLabController _sut = new();

    public InstantAnalysisLabControllerTests()
    {
        _factory.Setup(x => x.Resolve(It.IsAny<Sport>())).Returns(_client.Object);
    }

    [Fact]
    public async Task Baseball_IsRejected_OnEveryRoute_WithoutCallingProducer()
    {
        var id = Guid.NewGuid();

        (await _sut.RunLab("baseball", "mlb", id, _factory.Object, CancellationToken.None))
            .Result.Should().BeOfType<BadRequestObjectResult>();
        (await _sut.GetRuns("baseball", "mlb", id, _factory.Object, CancellationToken.None))
            .Result.Should().BeOfType<BadRequestObjectResult>();
        (await _sut.GetRun("baseball", "mlb", id, _factory.Object, CancellationToken.None))
            .Result.Should().BeOfType<BadRequestObjectResult>();
        (await _sut.Rate("baseball", "mlb", id, new RatePlayAnalysisRequest(3), _factory.Object, CancellationToken.None))
            .Result.Should().BeOfType<BadRequestObjectResult>();

        _factory.Verify(x => x.Resolve(It.IsAny<Sport>()), Times.Never);
    }

    [Theory]
    [InlineData("ncaa", Sport.FootballNcaa)]
    [InlineData("nfl", Sport.FootballNfl)]
    public async Task Football_IsRelayedToThatLeaguesProducer(string league, Sport mode)
    {
        var contestId = Guid.NewGuid();
        _client.Setup(x => x.GetInstantAnalysisLabRuns(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<PlayAnalysisRunSummaryDto>>([]));

        var result = await _sut.GetRuns("football", league, contestId, _factory.Object, CancellationToken.None);

        result.Result.Should().NotBeOfType<BadRequestObjectResult>();
        _factory.Verify(x => x.Resolve(mode), Times.Once);
    }
}
