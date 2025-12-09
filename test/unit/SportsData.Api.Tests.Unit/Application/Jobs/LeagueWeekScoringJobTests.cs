using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

using SportsData.Api.Application;
using SportsData.Api.Application.Jobs;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Jobs;

/// <summary>
/// Tests for LeagueWeekScoringJob - validates gap detection and backfill logic.
/// </summary>
public class LeagueWeekScoringJobTests : ApiTestBase<LeagueWeekScoringJob>
{
    private readonly Mock<ILogger<LeagueWeekScoringJob>> _loggerMock;
    private readonly Mock<IProvideCanonicalData> _canonicalDataMock;
    private readonly Mock<ILeagueWeekScoringService> _leagueWeekScoringServiceMock;

    public LeagueWeekScoringJobTests()
    {
        _loggerMock = new Mock<ILogger<LeagueWeekScoringJob>>();
        _canonicalDataMock = new Mock<IProvideCanonicalData>();
        _leagueWeekScoringServiceMock = new Mock<ILeagueWeekScoringService>();

        Mocker.Use(_loggerMock.Object);
        Mocker.Use(_canonicalDataMock.Object);
        Mocker.Use(_leagueWeekScoringServiceMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ScoresLeagueWeek_WhenNeverScored()
    {
        // Arrange
        var leagueId = Guid.NewGuid();
        var seasonWeekId = Guid.NewGuid();
        var seasonYear = 2024;
        var weekNumber = 1;

        var seasonWeeks = new List<SeasonWeek>
        {
            new()
            {
                Id = seasonWeekId,
                SeasonYear = seasonYear,
                WeekNumber = weekNumber
            }
        };

        var league = new PickemGroup
        {
            Id = leagueId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = leagueId,
            SeasonWeekId = seasonWeekId,
            ContestId = Guid.NewGuid(),
            SeasonYear = seasonYear,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow.AddDays(-1),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.SaveChangesAsync();

        _canonicalDataMock
            .Setup(x => x.GetCurrentAndLastWeekSeasonWeeks())
            .ReturnsAsync(seasonWeeks);

        _canonicalDataMock
            .Setup(x => x.GetFinalizedContestIds(seasonWeekId))
            .ReturnsAsync(new List<Guid> { matchup.ContestId });

        var sut = Mocker.CreateInstance<LeagueWeekScoringJob>();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _leagueWeekScoringServiceMock.Verify(
            x => x.ScoreLeagueWeekAsync(leagueId, seasonYear, weekNumber, default),
            Times.Once,
            "Should score league week when never scored before");
    }

    [Fact]
    public async Task ExecuteAsync_SkipsLeagueWeek_WhenRecentlyScored()
    {
        // Arrange
        var leagueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var seasonWeekId = Guid.NewGuid();
        var seasonYear = 2024;
        var weekNumber = 1;

        var seasonWeeks = new List<SeasonWeek>
        {
            new()
            {
                Id = seasonWeekId,
                SeasonYear = seasonYear,
                WeekNumber = weekNumber
            }
        };

        var league = new PickemGroup
        {
            Id = leagueId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = userId,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = leagueId,
            SeasonWeekId = seasonWeekId,
            ContestId = Guid.NewGuid(),
            SeasonYear = seasonYear,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow.AddDays(-1),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        // Already scored 30 minutes ago
        var existingResult = new PickemGroupWeekResult
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId,
            SeasonYear = seasonYear,
            SeasonWeek = weekNumber,
            TotalPoints = 5,
            CorrectPicks = 5,
            TotalPicks = 10,
            CalculatedUtc = DateTime.UtcNow.AddMinutes(-30),
            CreatedUtc = DateTime.UtcNow.AddMinutes(-30),
            CreatedBy = Guid.Empty
        };

        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.PickemGroupWeekResults.AddAsync(existingResult);
        await DataContext.SaveChangesAsync();

        _canonicalDataMock
            .Setup(x => x.GetCurrentAndLastWeekSeasonWeeks())
            .ReturnsAsync(seasonWeeks);

        _canonicalDataMock
            .Setup(x => x.GetFinalizedContestIds(seasonWeekId))
            .ReturnsAsync(new List<Guid>()); // Not all finalized

        var sut = Mocker.CreateInstance<LeagueWeekScoringJob>();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _leagueWeekScoringServiceMock.Verify(
            x => x.ScoreLeagueWeekAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), default),
            Times.Never,
            "Should skip league week when recently scored and not all contests finalized");
    }

    [Fact]
    public async Task ExecuteAsync_RescoresLeagueWeek_WhenAllContestsFinalizedAndOldCalculation()
    {
        // Arrange
        var leagueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var seasonWeekId = Guid.NewGuid();
        var seasonYear = 2024;
        var weekNumber = 1;

        var seasonWeeks = new List<SeasonWeek>
        {
            new()
            {
                Id = seasonWeekId,
                SeasonYear = seasonYear,
                WeekNumber = weekNumber
            }
        };

        var league = new PickemGroup
        {
            Id = leagueId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = userId,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = leagueId,
            SeasonWeekId = seasonWeekId,
            ContestId = Guid.NewGuid(),
            SeasonYear = seasonYear,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow.AddDays(-1),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        // Scored 2 hours ago, but all contests are now finalized
        var existingResult = new PickemGroupWeekResult
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId,
            SeasonYear = seasonYear,
            SeasonWeek = weekNumber,
            TotalPoints = 5,
            CorrectPicks = 5,
            TotalPicks = 10,
            CalculatedUtc = DateTime.UtcNow.AddHours(-2),
            CreatedUtc = DateTime.UtcNow.AddHours(-2),
            CreatedBy = Guid.Empty
        };

        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.PickemGroupWeekResults.AddAsync(existingResult);
        await DataContext.SaveChangesAsync();

        _canonicalDataMock
            .Setup(x => x.GetCurrentAndLastWeekSeasonWeeks())
            .ReturnsAsync(seasonWeeks);

        _canonicalDataMock
            .Setup(x => x.GetFinalizedContestIds(seasonWeekId))
            .ReturnsAsync(new List<Guid> { matchup.ContestId }); // All finalized

        var sut = Mocker.CreateInstance<LeagueWeekScoringJob>();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _leagueWeekScoringServiceMock.Verify(
            x => x.ScoreLeagueWeekAsync(leagueId, seasonYear, weekNumber, default),
            Times.Once,
            "Should rescore league week when all contests finalized and last calculation is old");
    }
}
