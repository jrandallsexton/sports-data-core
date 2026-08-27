namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsBySeasonWeekId;

/// <summary>
/// Matchups by SeasonWeek GUID — the PRECISE week identity. Unlike
/// number-based lookups there is no phase ambiguity: one SeasonWeek row
/// belongs to exactly one SeasonPhase. This is the query the league
/// schedule sync uses (its commands carry SeasonWeekId end-to-end), so
/// preseason and postseason league weeks sync correctly by construction.
/// </summary>
public record GetMatchupsBySeasonWeekIdQuery(Guid SeasonWeekId);
