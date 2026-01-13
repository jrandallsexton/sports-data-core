using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamMetrics;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.TeamCard.Queries.GetTeamMetrics;

public class GetTeamMetricsQueryHandlerTests : UnitTestBase<GetTeamMetricsQueryHandler>
{
    private readonly Mock<IProvideFranchises> _franchiseClientMock;
    private readonly Mock<IFranchiseClientFactory> _franchiseClientFactoryMock;

    public GetTeamMetricsQueryHandlerTests()
    {
        _franchiseClientMock = new Mock<IProvideFranchises>();
        _franchiseClientFactoryMock = Mocker.GetMock<IFranchiseClientFactory>();
        _franchiseClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_franchiseClientMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenMetricsExist()
    {
        // Arrange
        var franchiseSeasonId = Guid.NewGuid();
        var sport = Sport.FootballNcaa;
        var metrics = new FranchiseSeasonMetricsDto
        {
            FranchiseName = "Alabama Crimson Tide",
            FranchiseSlug = "alabama",
            SeasonYear = 2025,
            GamesPlayed = 12,
            Ypp = 6.5m,
            SuccessRate = 0.48m
        };

        _franchiseClientMock
            .Setup(x => x.GetFranchiseSeasonMetricsByFranchiseSeasonId(franchiseSeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        var handler = Mocker.CreateInstance<GetTeamMetricsQueryHandler>();
        var query = new GetTeamMetricsQuery { FranchiseSeasonId = franchiseSeasonId, Sport = sport };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FranchiseName.Should().Be("Alabama Crimson Tide");
        result.Value.FranchiseSlug.Should().Be("alabama");
        result.Value.SeasonYear.Should().Be(2025);
        result.Value.GamesPlayed.Should().Be(12);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenFranchiseSeasonIdIsEmpty()
    {
        // Arrange
        var sport = Sport.FootballNcaa;
        var handler = Mocker.CreateInstance<GetTeamMetricsQueryHandler>();
        var query = new GetTeamMetricsQuery { FranchiseSeasonId = Guid.Empty, Sport = sport };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        var failure = result as Failure<FranchiseSeasonMetricsDto>;
        failure!.Errors.Should().ContainSingle(e => e.PropertyName == nameof(query.FranchiseSeasonId));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenMetricsDoNotExist()
    {
        // Arrange
        var franchiseSeasonId = Guid.NewGuid();
        var sport = Sport.FootballNcaa;

        _franchiseClientMock
            .Setup(x => x.GetFranchiseSeasonMetricsByFranchiseSeasonId(franchiseSeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FranchiseSeasonMetricsDto?)null);

        var handler = Mocker.CreateInstance<GetTeamMetricsQueryHandler>();
        var query = new GetTeamMetricsQuery { FranchiseSeasonId = franchiseSeasonId, Sport = sport };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenExceptionThrown()
    {
        // Arrange
        var franchiseSeasonId = Guid.NewGuid();
        var sport = Sport.FootballNcaa;

        _franchiseClientMock
            .Setup(x => x.GetFranchiseSeasonMetricsByFranchiseSeasonId(franchiseSeasonId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var handler = Mocker.CreateInstance<GetTeamMetricsQueryHandler>();
        var query = new GetTeamMetricsQuery { FranchiseSeasonId = franchiseSeasonId, Sport = sport };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
    }
}
