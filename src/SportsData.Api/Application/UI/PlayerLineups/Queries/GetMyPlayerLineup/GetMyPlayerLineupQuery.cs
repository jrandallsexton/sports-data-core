namespace SportsData.Api.Application.UI.PlayerLineups.Queries.GetMyPlayerLineup;

public record GetMyPlayerLineupQuery(
    Guid LeagueId,
    Guid UserId,
    int SeasonYear,
    int SeasonWeek);
