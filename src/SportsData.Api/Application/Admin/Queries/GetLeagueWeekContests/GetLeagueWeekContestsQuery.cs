using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetLeagueWeekContests;

/// <summary>
/// Returns the sport and contest IDs for a given pickem league's week.
/// Used by the admin league-week replay endpoint to drive a SignalR
/// fan-out test across every MatchupCard on the picks page for a
/// chosen (league, week) — same identifiers the picks page itself
/// consumes via GET /ui/leagues/{leagueId}/matchups/{week}.
/// </summary>
public sealed record GetLeagueWeekContestsQuery(Guid LeagueId, int Week);

public sealed record GetLeagueWeekContestsResult(Sport Sport, IReadOnlyList<Guid> ContestIds);
