using Moq;

using SportsData.Api.Application.Jobs;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Jobs;

/// <summary>
/// Tests for LeagueWeekScoringJob — the sport-agnostic daily backstop.
///
/// The staleness predicate: a league week needs rescoring when any pick for
/// (league, year, week) was scored more recently than the last
/// PickemGroupWeekResult.CalculatedUtc for that tuple — or no result row
/// exists yet. No sport-specific endpoints or week semantics.
/// </summary>
public class LeagueWeekScoringJobTests : ApiTestBase<LeagueWeekScoringJob>
{
    private static readonly DateTime FixedUtcNow = new(2026, 5, 22, 9, 15, 0, DateTimeKind.Utc);
    private static readonly DateTime PickScoredAt = FixedUtcNow.AddHours(-1);
    private static readonly DateTime ResultCalculatedAfter = FixedUtcNow;
    private static readonly DateTime ResultCalculatedBefore = FixedUtcNow.AddHours(-2);

    [Fact]
    public async Task ExecuteAsync_Rescores_WhenNoResultRowExists()
    {
        // Arrange — picks scored but no PickemGroupWeekResult row at all.
        var leagueId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        const int seasonYear = 2026;
        const int seasonWeek = 1;

        await SeedMatchupAndScoredPickAsync(leagueId, contestId, seasonYear, seasonWeek, PickScoredAt);

        var service = Mocker.GetMock<ILeagueWeekScoringService>();

        var sut = Mocker.CreateInstance<LeagueWeekScoringJob>();

        // Act
        await sut.ExecuteAsync();

        // Assert
        service.Verify(x => x.ScoreLeagueWeekAsync(leagueId, seasonYear, seasonWeek, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Rescores_WhenPickScoredAfterLastCalculation()
    {
        // Arrange — pick scored AFTER the existing result row's CalculatedUtc.
        // Catches the "ContestScoringProcessor's tail leaderboard scoring failed
        // silently after picks were scored" scenario.
        var leagueId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        const int seasonYear = 2026;
        const int seasonWeek = 1;

        await SeedMatchupAndScoredPickAsync(leagueId, contestId, seasonYear, seasonWeek, PickScoredAt);
        await SeedResultAsync(leagueId, seasonYear, seasonWeek, ResultCalculatedBefore);

        var service = Mocker.GetMock<ILeagueWeekScoringService>();

        var sut = Mocker.CreateInstance<LeagueWeekScoringJob>();

        // Act
        await sut.ExecuteAsync();

        // Assert
        service.Verify(x => x.ScoreLeagueWeekAsync(leagueId, seasonYear, seasonWeek, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Skips_WhenResultIsCurrent()
    {
        // Arrange — pick scored BEFORE the existing result row's CalculatedUtc.
        // The leaderboard is fresh; no work to do.
        var leagueId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        const int seasonYear = 2026;
        const int seasonWeek = 1;

        await SeedMatchupAndScoredPickAsync(leagueId, contestId, seasonYear, seasonWeek, PickScoredAt);
        await SeedResultAsync(leagueId, seasonYear, seasonWeek, ResultCalculatedAfter);

        var service = Mocker.GetMock<ILeagueWeekScoringService>();

        var sut = Mocker.CreateInstance<LeagueWeekScoringJob>();

        // Act
        await sut.ExecuteAsync();

        // Assert
        service.Verify(
            x => x.ScoreLeagueWeekAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), default),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Skips_WhenNoScoredPicks()
    {
        // Arrange — matchup exists but no UserPicks have ScoredAt set.
        // Nothing has been scored, so no backstop work is needed.
        var leagueId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        const int seasonYear = 2026;
        const int seasonWeek = 1;

        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = leagueId,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = contestId,
            SeasonYear = seasonYear,
            SeasonWeek = seasonWeek,
            StartDateUtc = FixedUtcNow.AddDays(-1),
            CreatedUtc = FixedUtcNow,
            CreatedBy = Guid.Empty
        };

        var unscoredPick = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            ContestId = contestId,
            ScoredAt = null,
            CreatedUtc = FixedUtcNow,
            CreatedBy = Guid.Empty
        };

        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.UserPicks.AddAsync(unscoredPick);
        await DataContext.SaveChangesAsync();

        var service = Mocker.GetMock<ILeagueWeekScoringService>();

        var sut = Mocker.CreateInstance<LeagueWeekScoringJob>();

        // Act
        await sut.ExecuteAsync();

        // Assert
        service.Verify(
            x => x.ScoreLeagueWeekAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), default),
            Times.Never);
    }

    private async Task SeedMatchupAndScoredPickAsync(
        Guid leagueId,
        Guid contestId,
        int seasonYear,
        int seasonWeek,
        DateTime pickScoredAt)
    {
        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = leagueId,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = contestId,
            SeasonYear = seasonYear,
            SeasonWeek = seasonWeek,
            StartDateUtc = FixedUtcNow.AddDays(-1),
            CreatedUtc = FixedUtcNow,
            CreatedBy = Guid.Empty
        };

        var pick = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            ContestId = contestId,
            ScoredAt = pickScoredAt,
            CreatedUtc = FixedUtcNow,
            CreatedBy = Guid.Empty
        };

        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.UserPicks.AddAsync(pick);
        await DataContext.SaveChangesAsync();
    }

    private async Task SeedResultAsync(
        Guid leagueId,
        int seasonYear,
        int seasonWeek,
        DateTime calculatedUtc)
    {
        var result = new PickemGroupWeekResult
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = Guid.NewGuid(),
            SeasonYear = seasonYear,
            SeasonWeek = seasonWeek,
            TotalPoints = 0,
            CorrectPicks = 0,
            TotalPicks = 0,
            CalculatedUtc = calculatedUtc,
            CreatedUtc = calculatedUtc,
            CreatedBy = Guid.Empty
        };

        await DataContext.PickemGroupWeekResults.AddAsync(result);
        await DataContext.SaveChangesAsync();
    }
}
