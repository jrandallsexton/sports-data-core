namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;

public class GetLeagueWeekMatchupsQuery
{
    public required Guid UserId { get; init; }
    public required Guid LeagueId { get; init; }
    public required int Week { get; init; }
}
