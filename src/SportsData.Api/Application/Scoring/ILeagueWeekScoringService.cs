namespace SportsData.Api.Application.Scoring;

/// <summary>
/// Service for calculating weekly league scores and determining winners.
/// </summary>
public interface ILeagueWeekScoringService
{
    /// <summary>
    /// Scores a specific week for a specific league.
    /// </summary>
    Task ScoreLeagueWeekAsync(Guid leagueId, int seasonYear, int weekNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scores all active leagues for a specific week.
    /// </summary>
    Task ScoreAllLeaguesForWeekAsync(int seasonYear, int weekNumber, CancellationToken cancellationToken = default);
}
