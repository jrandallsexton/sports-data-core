using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Analytics.Queries.GetFranchiseSeasonMetrics;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Analytics.Queries.GetFranchiseSeasonMetrics;

public class GetFranchiseSeasonMetricsQueryHandlerTests : ApiTestBase<GetFranchiseSeasonMetricsQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnMetrics_WhenDataExists()
    {
        // Arrange
        var seasonYear = 2025;
        var sport = Sport.FootballNcaa;
        var expectedMetrics = new List<FranchiseSeasonMetricsDto>
        {
            new(),
            new()
        };

        var mockClient = new Mock<IProvideFranchises>();
        mockClient
            .Setup(x => x.GetFranchiseSeasonMetrics(seasonYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetrics);

        Mocker.GetMock<IFranchiseClientFactory>()
            .Setup(x => x.Resolve(sport))
            .Returns(mockClient.Object);

        var sut = Mocker.CreateInstance<GetFranchiseSeasonMetricsQueryHandler>();
        var query = new GetFranchiseSeasonMetricsQuery { SeasonYear = seasonYear, Sport = sport };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenNoDataExists()
    {
        // Arrange
        var seasonYear = 2025;
        var sport = Sport.FootballNcaa;

        var mockClient = new Mock<IProvideFranchises>();
        mockClient
            .Setup(x => x.GetFranchiseSeasonMetrics(seasonYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FranchiseSeasonMetricsDto>());

        Mocker.GetMock<IFranchiseClientFactory>()
            .Setup(x => x.Resolve(sport))
            .Returns(mockClient.Object);

        var sut = Mocker.CreateInstance<GetFranchiseSeasonMetricsQueryHandler>();
        var query = new GetFranchiseSeasonMetricsQuery { SeasonYear = seasonYear, Sport = sport };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
