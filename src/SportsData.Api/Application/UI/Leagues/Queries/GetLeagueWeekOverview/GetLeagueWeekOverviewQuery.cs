namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekOverview;

public class GetLeagueWeekOverviewQuery
{
    public required Guid LeagueId { get; init; }

    public required int Week { get; init; }
}
