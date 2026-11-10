namespace SportsData.Producer.Application.Athletes.Queries.GetAthleteSeasonLeaders;

/// <summary>
/// A season leaderboard for one athlete stat.
/// </summary>
/// <param name="SeasonYear">The season.</param>
/// <param name="Category">ESPN stat category name ("passing", "defensive", ...).</param>
/// <param name="Stat">ESPN stat name within the category ("passingYards", "sacks", ...).</param>
/// <param name="PerGame">Rank by total / games played instead of the season total.</param>
/// <param name="Limit">How many leaders to return.</param>
public record GetAthleteSeasonLeadersQuery(
    int SeasonYear,
    string Category,
    string Stat,
    bool PerGame = false,
    int Limit = 10);
