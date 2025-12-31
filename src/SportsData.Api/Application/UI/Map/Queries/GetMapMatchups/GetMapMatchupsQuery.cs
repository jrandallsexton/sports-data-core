namespace SportsData.Api.Application.UI.Map.Queries.GetMapMatchups;

public class GetMapMatchupsQuery
{
    public Guid? LeagueId { get; init; }

    public int? WeekNumber { get; init; }

    /// <summary>
    /// The season year for querying matchups by week. Defaults to current year if not specified.
    /// </summary>
    public int? SeasonYear { get; init; }
}
