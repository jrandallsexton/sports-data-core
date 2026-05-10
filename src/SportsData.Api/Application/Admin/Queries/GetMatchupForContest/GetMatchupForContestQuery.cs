using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetMatchupForContest;

/// <summary>
/// Returns one canonical matchup in the same shape the picks page consumes
/// (<see cref="UI.Leagues.Dtos.LeagueWeekMatchupsDto.MatchupForPickDto"/>),
/// without any league-context fields. Used by the SignalR debug pages so a
/// real MatchupCard can be rendered for a chosen contest.
/// </summary>
public sealed record GetMatchupForContestQuery(Guid ContestId, Sport Sport);
