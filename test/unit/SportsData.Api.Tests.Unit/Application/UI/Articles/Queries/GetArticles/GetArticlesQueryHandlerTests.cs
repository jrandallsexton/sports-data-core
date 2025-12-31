using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Articles.Queries.GetArticles;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Articles.Queries.GetArticles;

public class GetArticlesQueryHandlerTests : ApiTestBase<GetArticlesQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenCurrentSeasonWeekNotFound()
    {
        // Arrange
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
