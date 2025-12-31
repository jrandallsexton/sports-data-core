using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using SportsData.Api.Application.UI.Articles.Queries.GetArticles;
using SportsData.Api.Config;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Articles.Queries.GetArticles;

public class GetArticlesQueryHandlerTests : ApiTestBase<GetArticlesQueryHandler>
{
    private void SetupApiConfig()
    {
        var apiConfig = new ApiConfig { BaseUrl = "http://localhost:5262" };
        Mocker.GetMock<IOptions<ApiConfig>>()
            .Setup(x => x.Value)
            .Returns(apiConfig);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenCurrentSeasonWeekNotFound()
    {
        // Arrange
        SetupApiConfig();
        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetCurrentSeasonWeek())
            .ReturnsAsync((SeasonWeek?)null);

        var sut = Mocker.CreateInstance<GetArticlesQueryHandler>();
        var query = new GetArticlesQuery();

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenNoArticlesExist()
    {
        // Arrange
        SetupApiConfig();
        var seasonWeek = new SeasonWeek
        {
            Id = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            WeekNumber = 5,
            SeasonYear = 2025,
            SeasonPhase = "Regular"
        };

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetCurrentSeasonWeek())
            .ReturnsAsync(seasonWeek);

        var sut = Mocker.CreateInstance<GetArticlesQueryHandler>();
        var query = new GetArticlesQuery();

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Articles.Should().BeEmpty();
        result.Value.SeasonWeekNumber.Should().Be(5);
        result.Value.SeasonYear.Should().Be(2025);
    }
}
