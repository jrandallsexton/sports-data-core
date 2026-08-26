namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsForSeasonWeek;

/// <summary>
/// Week numbers repeat across phases within one season year (NFL 2026:
/// Preseason 1-4, Regular Season 1-18, Postseason 1-5). The phase type
/// code disambiguates; 2 = regular season, the default every pick'em
/// surface wants. A future playoff/CFP mode passes 3.
/// </summary>
public record GetMatchupsForSeasonWeekQuery(int SeasonYear, int SeasonWeekNumber, int SeasonPhaseTypeCode = 2);
