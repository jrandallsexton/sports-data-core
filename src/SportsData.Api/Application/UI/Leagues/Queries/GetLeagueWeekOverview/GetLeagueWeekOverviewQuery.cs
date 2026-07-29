namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekOverview;

public class GetLeagueWeekOverviewQuery
{
    public required Guid LeagueId { get; init; }

    /// <summary>The caller — must be a member of the league.</summary>
    public required Guid UserId { get; init; }

    public required int Week { get; init; }
}
