namespace SportsData.Producer.Application.Franchises.Queries.GetTeamFinalizedGames;

public record GetTeamFinalizedGamesQuery(string Slug, int SeasonYear, DateTime? AsOfDate);
