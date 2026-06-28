using FluentAssertions;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Map.Queries.GetMapMatchups;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Map.Queries.GetMapMatchups;

public class GetMapMatchupsQueryHandlerTests : ApiTestBase<GetMapMatchupsQueryHandler>
{
    // Fixed "now" so the SeasonYear default (UtcNow().Year) is deterministic.
    private static readonly DateTime FixedNow = new(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IProvideContests> _contestClientMock = new();

    public GetMapMatchupsQueryHandlerTests()
    {
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClientMock.Object);

        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);
    }

    private static Matchup CreateMatchup() => new()
    {
        ContestId = Guid.NewGuid(),
        AwaySlug = "away-team",
        HomeSlug = "home-team"
    };

    /// <summary>
    /// Seeds a PickemGroup so the handler can resolve a league (and its sport).
    /// The map is league-scoped — every reachable path requires one.
    /// </summary>
    private async Task<PickemGroup> SeedLeagueAsync(Sport sport = Sport.FootballNcaa)
    {
        var league = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Test League",
            Sport = sport,
            League = League.NCAAF,
            CommissionerUserId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };

        DataContext.PickemGroups.Add(league);
        await DataContext.SaveChangesAsync();
        return league;
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenLeagueDoesNotExist()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetMapMatchupsQueryHandler>();
        var query = new GetMapMatchupsQuery { LeagueId = Guid.NewGuid() };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGetCurrentWeekMatchups_WhenNoWeekProvided()
    {
        // Arrange
        var league = await SeedLeagueAsync();
        var expectedMatchups = new List<Matchup>
        {
            CreateMatchup(),
            CreateMatchup()
        };

        _contestClientMock
            .Setup(x => x.GetMatchupsForCurrentWeek(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<Matchup>>(expectedMatchups));

        var sut = Mocker.CreateInstance<GetMapMatchupsQueryHandler>();
        var query = new GetMapMatchupsQuery { LeagueId = league.Id };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Matchups.Should().HaveCount(2);
        _contestClientMock
            .Verify(x => x.GetMatchupsForCurrentWeek(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldResolveClientForLeagueSport()
    {
        // Arrange
        var league = await SeedLeagueAsync(Sport.BaseballMlb);

        _contestClientMock
            .Setup(x => x.GetMatchupsForCurrentWeek(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<Matchup>>(new List<Matchup>()));

        var sut = Mocker.CreateInstance<GetMapMatchupsQueryHandler>();
        var query = new GetMapMatchupsQuery { LeagueId = league.Id };

        // Act
        await sut.ExecuteAsync(query);

        // Assert — resolved by the league's sport, not a hardcoded default.
        Mocker.GetMock<IContestClientFactory>()
            .Verify(x => x.Resolve(Sport.BaseballMlb), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGetMatchupsBySeasonWeek_WhenWeekNumberProvided()
    {
        // Arrange
        var league = await SeedLeagueAsync();
        var weekNumber = 5;
        var expectedMatchups = new List<Matchup>
        {
            CreateMatchup()
        };

        _contestClientMock
            .Setup(x => x.GetMatchupsForSeasonWeek(FixedNow.Year, weekNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<Matchup>>(expectedMatchups));

        var sut = Mocker.CreateInstance<GetMapMatchupsQueryHandler>();
        var query = new GetMapMatchupsQuery { LeagueId = league.Id, WeekNumber = weekNumber };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert — SeasonYear defaults to the current year from IDateTimeProvider.
        result.IsSuccess.Should().BeTrue();
        result.Value.Matchups.Should().HaveCount(1);
        _contestClientMock
            .Verify(x => x.GetMatchupsForSeasonWeek(FixedNow.Year, weekNumber, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseProvidedSeasonYear_WhenSpecified()
    {
        // Arrange
        var league = await SeedLeagueAsync();
        var weekNumber = 5;
        var seasonYear = 2024;

        _contestClientMock
            .Setup(x => x.GetMatchupsForSeasonWeek(seasonYear, weekNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<Matchup>>(new List<Matchup>()));

        var sut = Mocker.CreateInstance<GetMapMatchupsQueryHandler>();
        var query = new GetMapMatchupsQuery { LeagueId = league.Id, WeekNumber = weekNumber, SeasonYear = seasonYear };

        // Act
        await sut.ExecuteAsync(query);

        // Assert
        _contestClientMock
            .Verify(x => x.GetMatchupsForSeasonWeek(seasonYear, weekNumber, It.IsAny<CancellationToken>()), Times.Once);
    }
}
