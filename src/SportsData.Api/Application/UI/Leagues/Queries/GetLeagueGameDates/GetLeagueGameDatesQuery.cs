namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueGameDates;

/// <summary>
/// Game dates for a sport/league within an optional [From, To] window. The
/// sport/league route slugs (mirroring POST /ui/leagues/{sport}/{league}) are
/// resolved to the canonical <c>Sport</c> in the handler.
/// </summary>
public record GetLeagueGameDatesQuery(string Sport, string League, DateTime? From, DateTime? To);
