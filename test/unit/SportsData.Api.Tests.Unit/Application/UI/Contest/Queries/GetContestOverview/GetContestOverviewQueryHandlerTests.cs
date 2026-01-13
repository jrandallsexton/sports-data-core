using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Contest.Queries.GetContestOverview;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Contest.Queries.GetContestOverview;

public class GetContestOverviewQueryHandlerTests : ApiTestBase<GetContestOverviewQueryHandler>
{
    private readonly Mock<IProvideContests> _contestClientMock;
    private readonly Mock<IContestClientFactory> _contestClientFactoryMock;

    public GetContestOverviewQueryHandlerTests()
    {
        _contestClientMock = new Mock<IProvideContests>();
        _contestClientFactoryMock = Mocker.GetMock<IContestClientFactory>();
        _contestClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClientMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenContestExists()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var sport = Sport.FootballNcaa;
        var expectedOverview = new ContestOverviewDto();

        _contestClientMock
            .Setup(x => x.GetContestOverviewByContestId(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOverview);

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();
        var query = new GetContestOverviewQuery { ContestId = contestId, Sport = sport };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeSameAs(expectedOverview);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenContestDoesNotExist()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var sport = Sport.FootballNcaa;

        _contestClientMock
            .Setup(x => x.GetContestOverviewByContestId(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContestOverviewDto?)null);

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();
        var query = new GetContestOverviewQuery { ContestId = contestId, Sport = sport };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
