using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Analytics.Queries.GetFranchiseSeasonMetrics;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Dtos.Canonical;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Analytics.Queries.GetFranchiseSeasonMetrics;

public class GetFranchiseSeasonMetricsQueryHandlerTests : ApiTestBase<GetFranchiseSeasonMetricsQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnMetrics_WhenDataExists()
    {
        // Arrange
        var seasonYear = 2025;
        var expectedMetrics = new List<FranchiseSeasonMetricsDto>
        {
            new(),
            new()
        };

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetFranchiseSeasonMetricsBySeasonYear(seasonYear))
            .ReturnsAsync(expectedMetrics);

        var sut = Mocker.CreateInstance<GetFranchiseSeasonMetricsQueryHandler>();
        var query = new GetFranchiseSeasonMetricsQuery { SeasonYear = seasonYear };

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

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetFranchiseSeasonMetricsBySeasonYear(seasonYear))
            .ReturnsAsync(new List<FranchiseSeasonMetricsDto>());

        var sut = Mocker.CreateInstance<GetFranchiseSeasonMetricsQueryHandler>();
        var query = new GetFranchiseSeasonMetricsQuery { SeasonYear = seasonYear };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
