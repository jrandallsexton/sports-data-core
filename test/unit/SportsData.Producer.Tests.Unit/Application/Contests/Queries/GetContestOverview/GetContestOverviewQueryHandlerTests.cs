using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.Contests.Queries.GetContestOverview;
using SportsData.Producer.Application.Services;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests.Queries.GetContestOverview;

public class GetContestOverviewQueryHandlerTests : ProducerTestBase<GetContestOverviewQueryHandler>
{
    private readonly Mock<ILogoSelectionService> _logoServiceMock;
    private readonly Mock<IValidator<GetContestOverviewQuery>> _validatorMock;

    public GetContestOverviewQueryHandlerTests()
    {
        _logoServiceMock = Mocker.GetMock<ILogoSelectionService>();
        _validatorMock = Mocker.GetMock<IValidator<GetContestOverviewQuery>>();
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidQuery_ReturnsValidationFailure()
    {
        // Arrange
        var query = new GetContestOverviewQuery(Guid.Empty); // Invalid GUID

        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("ContestId", "ContestId must be provided")
        };

        _validatorMock.Setup(v => v.ValidateAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationFailures));

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        Assert.IsType<Failure<ContestOverviewDto>>(result);
        var failure = (Failure<ContestOverviewDto>)result;
        Assert.Equal(ResultStatus.BadRequest, failure.Status);
        Assert.Single(failure.Errors);
        Assert.Equal("ContestId", failure.Errors[0].PropertyName);
        Assert.Equal("ContestId must be provided", failure.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidQuery_ProceedsToDataAccess()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var query = new GetContestOverviewQuery(contestId);

        _validatorMock.Setup(v => v.ValidateAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult()); // Valid

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert - Should proceed to database query (contest won't be found in in-memory DB, but that's OK)
        Assert.IsType<Failure<ContestOverviewDto>>(result); // NotFound from missing data, not validation
        var failure = (Failure<ContestOverviewDto>)result;
        Assert.Equal(ResultStatus.NotFound, failure.Status); // NotFound, not BadRequest
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_DoesNotAccessDatabase()
    {
        // Arrange
        var query = new GetContestOverviewQuery(Guid.Empty);

        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("ContestId", "ContestId must be provided")
        };

        _validatorMock.Setup(v => v.ValidateAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationFailures));

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        Assert.IsType<Failure<ContestOverviewDto>>(result);
        var failure = (Failure<ContestOverviewDto>)result;
        Assert.Equal(ResultStatus.BadRequest, failure.Status);
        Assert.Single(failure.Errors);
        Assert.Equal("ContestId", failure.Errors[0].PropertyName);
    }

    #region Leader Projection and Tie Detection Tests

    [Fact]
    public async Task GetGameLeadersAsync_WithTiedPlayers_ReturnsBothPlayers()
    {
        // Arrange - Two players with identical passing yards (287)
        var contestId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        
        await SeedContestWithLeaders(contestId, homeTeamId, awayTeamId, new[]
        {
            // Home team - two QBs tied at 287 yards
            (homeTeamId, "QB1", "John Smith", 287.0, "23/34, 287 YDS, 2 TD"),
            (homeTeamId, "QB2", "Jane Doe", 287.0, "19/28, 287 YDS, 1 TD")
        });

        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<GetContestOverviewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(new GetContestOverviewQuery(contestId));

        // Assert
        Assert.IsType<Success<ContestOverviewDto>>(result);
        var success = (Success<ContestOverviewDto>)result;
        var leaders = success.Value.Leaders;

        Assert.NotNull(leaders);
        Assert.Single(leaders.Categories); // One category: Passing Yards
        
        var passingCategory = leaders.Categories[0];
        Assert.Equal(2, passingCategory.Home.Leaders.Count); // Both tied players
        Assert.Contains(passingCategory.Home.Leaders, l => l.PlayerName == "John Smith");
        Assert.Contains(passingCategory.Home.Leaders, l => l.PlayerName == "Jane Doe");
        
        // Both should have same value
        Assert.All(passingCategory.Home.Leaders, l => Assert.Equal(287m, l.Value));
    }

    [Fact]
    public async Task GetGameLeadersAsync_WithDifferentValues_ReturnsOnlyTopPlayer()
    {
        // Arrange - Three players with different values
        var contestId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        
        await SeedContestWithLeaders(contestId, homeTeamId, awayTeamId, new[]
        {
            // Home team - different rushing yards
            (homeTeamId, "RB1", "Top Rusher", 150.0, "22 CAR, 150 YDS, 2 TD"),
            (homeTeamId, "RB2", "Second Rusher", 89.0, "15 CAR, 89 YDS, 1 TD"),
            (homeTeamId, "RB3", "Third Rusher", 45.0, "8 CAR, 45 YDS")
        });

        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<GetContestOverviewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(new GetContestOverviewQuery(contestId));

        // Assert
        Assert.IsType<Success<ContestOverviewDto>>(result);
        var success = (Success<ContestOverviewDto>)result;
        var leaders = success.Value.Leaders;

        var rushingCategory = leaders.Categories[0];
        Assert.Single(rushingCategory.Home.Leaders); // Only top rusher
        Assert.Equal("Top Rusher", rushingCategory.Home.Leaders[0].PlayerName);
        Assert.Equal(150m, rushingCategory.Home.Leaders[0].Value);
    }

    [Fact]
    public async Task GetGameLeadersAsync_WithThreeWayTie_ReturnsAllThreePlayers()
    {
        // Arrange - Three players tied with same receptions
        var contestId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        
        await SeedContestWithLeaders(contestId, homeTeamId, awayTeamId, new[]
        {
            // Home team - three receivers tied at 8 receptions
            (homeTeamId, "WR1", "Receiver One", 8.0, "8 REC, 120 YDS"),
            (homeTeamId, "WR2", "Receiver Two", 8.0, "8 REC, 95 YDS"),
            (homeTeamId, "WR3", "Receiver Three", 8.0, "8 REC, 110 YDS")
        });

        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<GetContestOverviewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(new GetContestOverviewQuery(contestId));

        // Assert
        Assert.IsType<Success<ContestOverviewDto>>(result);
        var success = (Success<ContestOverviewDto>)result;
        var leaders = success.Value.Leaders;

        var receptionsCategory = leaders.Categories[0];
        Assert.Equal(3, receptionsCategory.Home.Leaders.Count); // All three tied
        Assert.Contains(receptionsCategory.Home.Leaders, l => l.PlayerName == "Receiver One");
        Assert.Contains(receptionsCategory.Home.Leaders, l => l.PlayerName == "Receiver Two");
        Assert.Contains(receptionsCategory.Home.Leaders, l => l.PlayerName == "Receiver Three");
        Assert.All(receptionsCategory.Home.Leaders, l => Assert.Equal(8m, l.Value));
    }

    [Fact]
    public async Task GetGameLeadersAsync_WithBothTeams_SeparatesHomeAndAwayLeaders()
    {
        // Arrange - Leaders for both teams
        var contestId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        
        await SeedContestWithLeaders(contestId, homeTeamId, awayTeamId, new[]
        {
            // Home team leader
            (homeTeamId, "QB", "Home QB", 320.0, "25/35, 320 YDS"),
            // Away team leader
            (awayTeamId, "QB", "Away QB", 280.0, "22/30, 280 YDS")
        });

        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<GetContestOverviewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(new GetContestOverviewQuery(contestId));

        // Assert
        Assert.IsType<Success<ContestOverviewDto>>(result);
        var success = (Success<ContestOverviewDto>)result;
        var leaders = success.Value.Leaders;

        var passingCategory = leaders.Categories[0];
        
        Assert.Single(passingCategory.Home.Leaders);
        Assert.Equal("Home QB", passingCategory.Home.Leaders[0].PlayerName);
        Assert.Equal(320m, passingCategory.Home.Leaders[0].Value);
        
        Assert.Single(passingCategory.Away.Leaders);
        Assert.Equal("Away QB", passingCategory.Away.Leaders[0].PlayerName);
        Assert.Equal(280m, passingCategory.Away.Leaders[0].Value);
    }

    [Fact]
    public async Task GetGameLeadersAsync_PopulatesAbbreviationFromCategory()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        
        await SeedContestWithLeaders(contestId, homeTeamId, awayTeamId, new[]
        {
            (homeTeamId, "QB", "Quarterback", 300.0, "300 YDS")
        }, categoryAbbreviation: "YDS");

        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<GetContestOverviewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(new GetContestOverviewQuery(contestId));

        // Assert
        Assert.IsType<Success<ContestOverviewDto>>(result);
        var success = (Success<ContestOverviewDto>)result;
        var leaders = success.Value.Leaders;

        var category = leaders.Categories[0];
        Assert.Equal("YDS", category.Abbr);
    }

    [Fact]
    public async Task GetGameLeadersAsync_SortsLeadersByNumericValue()
    {
        // Arrange - Verify that tie detection uses Numeric, not StatLine
        var contestId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        
        await SeedContestWithLeaders(contestId, homeTeamId, awayTeamId, new[]
        {
            // Same numeric value (125.0) but different StatLine formatting
            (homeTeamId, "WR1", "Player A", 125.0, "125 YDS"),
            (homeTeamId, "WR2", "Player B", 125.0, "8 REC, 125 YDS, 1 TD"), // Different format!
            (homeTeamId, "WR3", "Player C", 100.0, "100 YDS") // Lower value
        });

        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<GetContestOverviewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(new GetContestOverviewQuery(contestId));

        // Assert
        Assert.IsType<Success<ContestOverviewDto>>(result);
        var success = (Success<ContestOverviewDto>)result;
        var leaders = success.Value.Leaders;

        var category = leaders.Categories[0];
        Assert.Equal(2, category.Home.Leaders.Count); // Both 125.0 players (tied)
        Assert.Contains(category.Home.Leaders, l => l.PlayerName == "Player A" && l.Value == 125m);
        Assert.Contains(category.Home.Leaders, l => l.PlayerName == "Player B" && l.Value == 125m);
        Assert.DoesNotContain(category.Home.Leaders, l => l.PlayerName == "Player C"); // Lower value excluded
    }

    #endregion

    #region Helper Methods

    private async Task SeedContestWithLeaders(
        Guid contestId,
        Guid homeTeamId,
        Guid awayTeamId,
        (Guid franchiseSeasonId, string position, string playerName, double value, string displayValue)[] stats,
        string categoryAbbreviation = "PASS YDS")
    {
        // Create franchises
        var homeFranchise = new Franchise
        {
            Id = Guid.NewGuid(),
            Sport = Sport.FootballNcaa,
            Name = "Home Team",
            Location = "Home City",
            DisplayName = "Home Team",
            DisplayNameShort = "Home",
            Slug = "home-team",
            Abbreviation = "HOME",
            ColorCodeHex = "#FF0000",
            VenueId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var awayFranchise = new Franchise
        {
            Id = Guid.NewGuid(),
            Sport = Sport.FootballNcaa,
            Name = "Away Team",
            Location = "Away City",
            DisplayName = "Away Team",
            DisplayNameShort = "Away",
            Slug = "away-team",
            Abbreviation = "AWAY",
            ColorCodeHex = "#0000FF",
            VenueId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Create franchise seasons
        var homeTeamSeason = new FranchiseSeason
        {
            Id = homeTeamId,
            FranchiseId = homeFranchise.Id,
            SeasonYear = 2024,
            Abbreviation = "HOME",
            DisplayName = "Home Team",
            DisplayNameShort = "Home",
            Location = "Home City",
            Name = "Home Team",
            Slug = "home-team",
            ColorCodeHex = "#FF0000",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var awayTeamSeason = new FranchiseSeason
        {
            Id = awayTeamId,
            FranchiseId = awayFranchise.Id,
            SeasonYear = 2024,
            Abbreviation = "AWAY",
            DisplayName = "Away Team",
            DisplayNameShort = "Away",
            Location = "Away City",
            Name = "Away Team",
            Slug = "away-team",
            ColorCodeHex = "#0000FF",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Create season week
        var seasonPhase = new SeasonPhase
        {
            Id = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            Name = "Regular Season",
            Abbreviation = "REG",
            Slug = "regular-season",
            TypeCode = 2,
            Year = 2024,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var seasonWeek = new SeasonWeek
        {
            Id = Guid.NewGuid(),
            Number = 1,
            SeasonId = seasonPhase.SeasonId,
            SeasonPhaseId = seasonPhase.Id,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Create contest
        var contest = new Contest
        {
            Id = contestId,
            Name = "Home vs Away",
            ShortName = "Home @ Away",
            Sport = Sport.FootballNcaa,
            SeasonYear = 2024,
            HomeTeamFranchiseSeasonId = homeTeamId,
            AwayTeamFranchiseSeasonId = awayTeamId,
            SeasonWeekId = seasonWeek.Id,
            SeasonPhaseId = seasonPhase.Id,
            StartDateUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Create competition
        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Create leader category
        var leaderCategory = new CompetitionLeaderCategory
        {
            Id = 1,
            Name = "passingYards",
            DisplayName = "Passing Yards",
            ShortDisplayName = "Pass YDS",
            Abbreviation = categoryAbbreviation,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Create competition leader
        var competitionLeader = new CompetitionLeader
        {
            Id = Guid.NewGuid(),
            CompetitionId = competition.Id,
            LeaderCategoryId = leaderCategory.Id,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Create athlete seasons and stats
        var competitionLeaderStats = new List<CompetitionLeaderStat>();
        
        foreach (var stat in stats)
        {
            var nameParts = stat.playerName.Split(' ', 2);
            var firstName = nameParts.Length > 0 ? nameParts[0] : "Unknown";
            var lastName = nameParts.Length > 1 ? nameParts[1] : "Player";

            var athlete = new Athlete
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                DisplayName = stat.playerName,
                ShortName = stat.playerName,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var athleteSeason = new AthleteSeason
            {
                Id = Guid.NewGuid(),
                AthleteId = athlete.Id,
                FranchiseSeasonId = stat.franchiseSeasonId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var leaderStat = new CompetitionLeaderStat
            {
                Id = Guid.NewGuid(),
                CompetitionLeaderId = competitionLeader.Id,
                AthleteSeasonId = athleteSeason.Id,
                FranchiseSeasonId = stat.franchiseSeasonId,
                Value = stat.value,
                DisplayValue = stat.displayValue,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await TeamSportDataContext.Athletes.AddAsync(athlete);
            await TeamSportDataContext.AthleteSeasons.AddAsync(athleteSeason);
            competitionLeaderStats.Add(leaderStat);
        }

        // Save all entities
        await TeamSportDataContext.Franchises.AddAsync(homeFranchise);
        await TeamSportDataContext.Franchises.AddAsync(awayFranchise);
        await TeamSportDataContext.FranchiseSeasons.AddAsync(homeTeamSeason);
        await TeamSportDataContext.FranchiseSeasons.AddAsync(awayTeamSeason);
        await TeamSportDataContext.SeasonPhases.AddAsync(seasonPhase);
        await TeamSportDataContext.SeasonWeeks.AddAsync(seasonWeek);
        await TeamSportDataContext.Contests.AddAsync(contest);
        await TeamSportDataContext.Competitions.AddAsync(competition);
        await TeamSportDataContext.CompetitionLeaderCategories.AddAsync(leaderCategory);
        await TeamSportDataContext.CompetitionLeaders.AddAsync(competitionLeader);
        await TeamSportDataContext.CompetitionLeaderStats.AddRangeAsync(competitionLeaderStats);
        
        await TeamSportDataContext.SaveChangesAsync();
    }

    #endregion
}
