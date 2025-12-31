using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Map.Queries.GetMapMatchups;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Map.Queries.GetMapMatchups;

public class GetMapMatchupsQueryHandlerTests : ApiTestBase<GetMapMatchupsQueryHandler>
{
    private static Matchup CreateMatchup() => new()
    {
        ContestId = Guid.NewGuid(),
        AwaySlug = "away-team",
        HomeSlug = "home-team"
    };

    [Fact]
    public async Task ExecuteAsync_ShouldGetCurrentWeekMatchups_WhenNoParametersProvided()
    {
        // Arrange
        var expectedMatchups = new List<Matchup>
        {
            CreateMatchup(),
            CreateMatchup()
        };

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetMatchupsForCurrentWeek())
            .ReturnsAsync(expectedMatchups);

        var sut = Mocker.CreateInstance<GetMapMatchupsQueryHandler>();
        var query = new GetMapMatchupsQuery();

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Matchups.Should().HaveCount(2);
        Mocker.GetMock<IProvideCanonicalData>()
            .Verify(x => x.GetMatchupsForCurrentWeek(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGetMatchupsBySeasonWeek_WhenWeekNumberProvided()
    {
        // Arrange
        var weekNumber = 5;
        var currentYear = DateTime.UtcNow.Year;
        var expectedMatchups = new List<Matchup>
        {
            CreateMatchup()
        };

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetMatchupsForSeasonWeek(currentYear, weekNumber))
            .ReturnsAsync(expectedMatchups);

        var sut = Mocker.CreateInstance<GetMapMatchupsQueryHandler>();
        var query = new GetMapMatchupsQuery { WeekNumber = weekNumber };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Matchups.Should().HaveCount(1);
        Mocker.GetMock<IProvideCanonicalData>()
            .Verify(x => x.GetMatchupsForSeasonWeek(currentYear, weekNumber), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseProvidedSeasonYear_WhenSpecified()
    {
        // Arrange
        var weekNumber = 5;
        var seasonYear = 2024;
        var expectedMatchups = new List<Matchup>();

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetMatchupsForSeasonWeek(seasonYear, weekNumber))
            .ReturnsAsync(expectedMatchups);

        var sut = Mocker.CreateInstance<GetMapMatchupsQueryHandler>();
        var query = new GetMapMatchupsQuery { WeekNumber = weekNumber, SeasonYear = seasonYear };

        // Act
        await sut.ExecuteAsync(query);

        // Assert
        Mocker.GetMock<IProvideCanonicalData>()
            .Verify(x => x.GetMatchupsForSeasonWeek(seasonYear, weekNumber), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGetCurrentWeekMatchups_WhenLeagueIdProvided()
    {
        // Arrange
        var leagueId = Guid.NewGuid();
        var expectedMatchups = new List<Matchup>
        {
            CreateMatchup()
        };

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetMatchupsForCurrentWeek())
            .ReturnsAsync(expectedMatchups);

        var sut = Mocker.CreateInstance<GetMapMatchupsQueryHandler>();
        var query = new GetMapMatchupsQuery { LeagueId = leagueId };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IProvideCanonicalData>()
            .Verify(x => x.GetMatchupsForCurrentWeek(), Times.Once);
    }
}
