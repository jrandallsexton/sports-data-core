using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using SportsData.Api.Application.UI.Articles.Queries.GetArticles;
using SportsData.Api.Config;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Season;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Articles.Queries.GetArticles;

public class GetArticlesQueryHandlerTests : ApiTestBase<GetArticlesQueryHandler>
{
    private readonly Mock<IProvideSeasons> _seasonClientMock = new();

    public GetArticlesQueryHandlerTests()
    {
        Mocker.GetMock<ISeasonClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_seasonClientMock.Object);
    }

    private void SetupApiConfig()
    {
        var apiConfig = new ApiConfig { BaseUrl = "http://localhost:5262", UserIdSystem = Guid.NewGuid() };
        Mocker.GetMock<IOptions<ApiConfig>>()
            .Setup(x => x.Value)
            .Returns(apiConfig);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenCurrentSeasonWeekNotFound()
    {
        // Arrange
        SetupApiConfig();
        _seasonClientMock
            .Setup(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<CanonicalSeasonWeekDto>(default!, ResultStatus.NotFound, []));

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
        var seasonWeek = new CanonicalSeasonWeekDto
        {
            Id = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            WeekNumber = 5,
            SeasonYear = 2025,
            SeasonPhase = "Regular"
        };

        _seasonClientMock
            .Setup(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<CanonicalSeasonWeekDto>(seasonWeek));

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
