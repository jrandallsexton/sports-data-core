namespace SportsData.Api.Application.Seasons.Queries.GetCurrentSeason;

public class GetCurrentSeasonQuery
{
    public required string Sport { get; init; }

    public required string League { get; init; }
}
