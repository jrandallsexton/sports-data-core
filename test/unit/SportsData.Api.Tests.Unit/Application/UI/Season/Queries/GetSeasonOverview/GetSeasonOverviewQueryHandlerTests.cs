using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Season.Queries.GetSeasonOverview;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Season;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Season.Queries.GetSeasonOverview;

public class GetSeasonOverviewQueryHandlerTests : ApiTestBase<GetSeasonOverviewQueryHandler>
{
    private readonly Mock<IProvideSeasons> _seasonClientMock;
    private readonly Mock<ISeasonClientFactory> _seasonClientFactoryMock;

    public GetSeasonOverviewQueryHandlerTests()
    {
        _seasonClientMock = new Mock<IProvideSeasons>();
        _seasonClientFactoryMock = Mocker.GetMock<ISeasonClientFactory>();
        _seasonClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_seasonClientMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenSeasonExists()
    {
        // Arrange
        var seasonYear = 2025;
        var sport = Sport.FootballNcaa;
        var expectedOverview = new SeasonOverviewDto { SeasonYear = seasonYear };

        _seasonClientMock
            .Setup(x => x.GetSeasonOverview(seasonYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<SeasonOverviewDto>(expectedOverview));

        var sut = Mocker.CreateInstance<GetSeasonOverviewQueryHandler>();
        var query = new GetSeasonOverviewQuery { SeasonYear = seasonYear, Sport = sport };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeSameAs(expectedOverview);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenSeasonDoesNotExist()
    {
        // Arrange
        var seasonYear = 2025;
        var sport = Sport.FootballNcaa;

        _seasonClientMock
            .Setup(x => x.GetSeasonOverview(seasonYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<SeasonOverviewDto>(
                default!,
                ResultStatus.NotFound,
                []));

        var sut = Mocker.CreateInstance<GetSeasonOverviewQueryHandler>();
        var query = new GetSeasonOverviewQuery { SeasonYear = seasonYear, Sport = sport };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
