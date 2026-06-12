namespace SportsData.Api.Application.UI.Results.Queries.GetSeasonResults;

public class GetSeasonResultsQuery
{
    public required string Sport { get; init; }     // "football"
    public required string League { get; init; }    // "ncaa"
    public required int SeasonYear { get; init; }   // 2025
}
