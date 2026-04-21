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
    public async Task ExecuteAsync_ShouldReturnEmptyDto_WhenMetricsDoNotExist()
    {
        // Metrics are a non-blocking enrichment surface — when the producer returns
        // null we surface that as Success(empty DTO) so the UI renders a friendly
        // empty state rather than a 500/NotFound.
        var franchiseSeasonId = Guid.NewGuid();
        var sport = Sport.FootballNcaa;

        _franchiseClientMock
            .Setup(x => x.GetFranchiseSeasonMetricsByFranchiseSeasonId(franchiseSeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FranchiseSeasonMetricsDto)null!);

        var handler = Mocker.CreateInstance<GetTeamMetricsQueryHandler>();
        var query = new GetTeamMetricsQuery { FranchiseSeasonId = franchiseSeasonId, Sport = sport };

        var result = await handler.ExecuteAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.FranchiseName.Should().BeNullOrEmpty();
        result.Value.GamesPlayed.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyDto_WhenNonCancellationExceptionThrown()
    {
        // A real producer-side error (network, missing sport client, etc.) also
        // resolves to Success(empty DTO) rather than a hard failure so the UI
        // stays graceful. The exception is logged at Error level elsewhere for
        // ops visibility — we only validate the API contract here.
        var franchiseSeasonId = Guid.NewGuid();
        var sport = Sport.FootballNcaa;

        _franchiseClientMock
            .Setup(x => x.GetFranchiseSeasonMetricsByFranchiseSeasonId(franchiseSeasonId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var handler = Mocker.CreateInstance<GetTeamMetricsQueryHandler>();
        var query = new GetTeamMetricsQuery { FranchiseSeasonId = franchiseSeasonId, Sport = sport };

        var result = await handler.ExecuteAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.FranchiseName.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRethrow_WhenOperationCanceled()
    {
        // Cancellation must propagate — the graceful catch is for non-cancel errors.
        var franchiseSeasonId = Guid.NewGuid();
        var sport = Sport.FootballNcaa;

        _franchiseClientMock
            .Setup(x => x.GetFranchiseSeasonMetricsByFranchiseSeasonId(franchiseSeasonId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var handler = Mocker.CreateInstance<GetTeamMetricsQueryHandler>();
        var query = new GetTeamMetricsQuery { FranchiseSeasonId = franchiseSeasonId, Sport = sport };

        var act = async () => await handler.ExecuteAsync(query);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
