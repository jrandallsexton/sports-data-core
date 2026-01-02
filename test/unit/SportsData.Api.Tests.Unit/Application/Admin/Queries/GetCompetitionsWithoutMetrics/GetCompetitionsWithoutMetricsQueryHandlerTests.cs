using FluentAssertions;

using Moq;

using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutMetrics;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Queries.GetCompetitionsWithoutMetrics;

public class GetCompetitionsWithoutMetricsQueryHandlerTests : ApiTestBase<GetCompetitionsWithoutMetricsQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenDataProviderReturnsResults()
    {
        // Arrange
        var expectedResults = new List<CompetitionWithoutMetricsDto>
        {
            new() { CompetitionId = Guid.NewGuid(), ContestName = "Test Competition 1" },
            new() { CompetitionId = Guid.NewGuid(), ContestName = "Test Competition 2" }
        };

        Mocker.GetMock<IProvideCanonicalAdminData>()
            .Setup(x => x.GetCompetitionsWithoutMetrics(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutMetricsQueryHandler>();
        var query = new GetCompetitionsWithoutMetricsQuery();

        // Act
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Should().BeOfType<Success<List<CompetitionWithoutMetricsDto>>>();
        var success = (Success<List<CompetitionWithoutMetricsDto>>)result;
        success.Value.Should().HaveCount(2);
        success.Value.Should().BeEquivalentTo(expectedResults);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenNoResultsFound()
    {
        // Arrange
        var expectedResults = new List<CompetitionWithoutMetricsDto>();

        Mocker.GetMock<IProvideCanonicalAdminData>()
            .Setup(x => x.GetCompetitionsWithoutMetrics(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutMetricsQueryHandler>();
        var query = new GetCompetitionsWithoutMetricsQuery();

        // Act
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var success = (Success<List<CompetitionWithoutMetricsDto>>)result;
        success.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenDataProviderThrowsException()
    {
        // Arrange
        Mocker.GetMock<IProvideCanonicalAdminData>()
            .Setup(x => x.GetCompetitionsWithoutMetrics(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutMetricsQueryHandler>();
        var query = new GetCompetitionsWithoutMetricsQuery();

        // Act
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        result.Should().BeOfType<Failure<List<CompetitionWithoutMetricsDto>>>();
        var failure = (Failure<List<CompetitionWithoutMetricsDto>>)result;
        failure.Errors.Should().Contain(e => e.ErrorMessage == "An error occurred while retrieving competitions without metrics");
        failure.Value.Should().BeEmpty();
    }
}
