using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Map.Queries.GetMapMatchups;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Map.Queries.GetMapMatchups;

public class GetMapMatchupsQueryHandlerTests : ApiTestBase<GetMapMatchupsQueryHandler>
{
    private readonly Mock<IProvideContests> _contestClientMock = new();

    public GetMapMatchupsQueryHandlerTests()
    {
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClientMock.Object);
    }
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

        _contestClientMock
            .Setup(x => x.GetMatchupsForCurrentWeek(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<Matchup>>(expectedMatchups));

        var sut = Mocker.CreateInstance<GetMapMatchupsQueryHandler>();
        var query = new GetMapMatchupsQuery();

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Matchups.Should().HaveCount(2);
        _contestClientMock
            .Verify(x => x.GetMatchupsForCurrentWeek(It.IsAny<CancellationToken>()), Times.Once);
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

        _contestClientMock
            .Setup(x => x.GetMatchupsForSeasonWeek(currentYear, weekNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<Matchup>>(expectedMatchups));

        var sut = Mocker.CreateInstance<GetMapMatchupsQueryHandler>();
        var query = new GetMapMatchupsQuery { WeekNumber = weekNumber };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Matchups.Should().HaveCount(1);
        _contestClientMock
            .Verify(x => x.GetMatchupsForSeasonWeek(currentYear, weekNumber, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseProvidedSeasonYear_WhenSpecified()
    {
        // Arrange
        var weekNumber = 5;
        var seasonYear = 2024;
        var expectedMatchups = new List<Matchup>();

        _contestClientMock
            .Setup(x => x.GetMatchupsForSeasonWeek(seasonYear, weekNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<Matchup>>(expectedMatchups));

        var sut = Mocker.CreateInstance<GetMapMatchupsQueryHandler>();
        var query = new GetMapMatchupsQuery { WeekNumber = weekNumber, SeasonYear = seasonYear };

        // Act
        await sut.ExecuteAsync(query);

        // Assert
        _contestClientMock
            .Verify(x => x.GetMatchupsForSeasonWeek(seasonYear, weekNumber, It.IsAny<CancellationToken>()), Times.Once);
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

        _contestClientMock
            .Setup(x => x.GetMatchupsForCurrentWeek(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<Matchup>>(expectedMatchups));

        var sut = Mocker.CreateInstance<GetMapMatchupsQueryHandler>();
        var query = new GetMapMatchupsQuery { LeagueId = leagueId };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _contestClientMock
            .Verify(x => x.GetMatchupsForCurrentWeek(It.IsAny<CancellationToken>()), Times.Once);
    }
}
