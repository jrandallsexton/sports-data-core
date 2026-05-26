namespace SportsData.Api.Application.UI.TeamCard.Queries.GetTeamFinalizedGames;

public class GetTeamFinalizedGamesQuery
{
    public required string Sport { get; init; }

    public required string League { get; init; }

    public required string Slug { get; init; }

    public required int SeasonYear { get; init; }

    public DateTime? AsOfDate { get; init; }
}
