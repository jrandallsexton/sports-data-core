namespace SportsData.Producer.Application.Athletes.Queries.GetAthleteMatchupSummaries;

/// <summary>
/// Athletes-by-position for the Player Pick'em roster builder grid.
/// </summary>
/// <param name="Position">Position abbreviation: QB, RB, WR, TE, or K.</param>
/// <param name="SeasonYear">The season being played (current-season blocks and opponent lookup).</param>
/// <param name="Week">The week whose opponent/matchup context is attached.</param>
public record GetAthleteMatchupSummariesQuery(string Position, int SeasonYear, int Week);
