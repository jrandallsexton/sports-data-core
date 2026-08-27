namespace SportsData.Producer.Application.Athletes.Queries.GetAthleteMatchupSummaries;

/// <summary>
/// Athletes-by-position for the Player Pick'em roster builder grid.
/// </summary>
/// <param name="Position">Position abbreviation: QB, RB, WR, TE, or K.</param>
/// <param name="SeasonYear">The season being played (current-season blocks and opponent lookup).</param>
/// <param name="Week">The week whose opponent/matchup context is attached.</param>
/// <param name="SeasonPhaseTypeCode">Phase the week number belongs to (1 Preseason, 2 Regular Season, 3 Postseason) — numbers restart per phase.</param>
public record GetAthleteMatchupSummariesQuery(string Position, int SeasonYear, int Week, int SeasonPhaseTypeCode = 2);
