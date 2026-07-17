using FluentAssertions;

using Moq;

using SportsData.Api.Application.Seasons.Queries.GetCurrentSeason;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Season;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Seasons.Queries.GetCurrentSeason;

public class GetCurrentSeasonQueryHandlerTests : ApiTestBase<GetCurrentSeasonQueryHandler>
{
    private readonly Mock<IProvideSeasons> _seasonClientMock;

    public GetCurrentSeasonQueryHandlerTests()
    {
        _seasonClientMock = new Mock<IProvideSeasons>();
        Mocker.GetMock<ISeasonClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_seasonClientMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ResolvesSportAndPassesThroughCurrentSeason()
    {
        // Arrange
        var expected = new CurrentSeasonDto
        {
            SeasonYear = 2026,
            Name = "2026 Season",
            Phases = [new SeasonPhaseDto { TypeCode = 2, Name = "Regular Season" }]
        };
        _seasonClientMock
            .Setup(x => x.GetCurrentSeason(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<CurrentSeasonDto>(expected));

        var sut = Mocker.CreateInstance<GetCurrentSeasonQueryHandler>();
        var query = new GetCurrentSeasonQuery { Sport = "football", League = "ncaa" };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert — the NCAA mode resolves and the DTO passes straight through.
        Mocker.GetMock<ISeasonClientFactory>().Verify(x => x.Resolve(Sport.FootballNcaa), Times.Once);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesNotFound()
    {
        // Arrange — season not sourced yet.
        _seasonClientMock
            .Setup(x => x.GetCurrentSeason(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<CurrentSeasonDto>(default!, ResultStatus.NotFound, []));

        var sut = Mocker.CreateInstance<GetCurrentSeasonQueryHandler>();
        var query = new GetCurrentSeasonQuery { Sport = "football", League = "nfl" };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedSportLeague_ReturnsBadRequest_WithoutCallingClient()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetCurrentSeasonQueryHandler>();
        var query = new GetCurrentSeasonQuery { Sport = "quidditch", League = "hogwarts" };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert — fails at mode resolution; never resolves a client.
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
        _seasonClientMock.Verify(x => x.GetCurrentSeason(It.IsAny<CancellationToken>()), Times.Never);
    }
}
