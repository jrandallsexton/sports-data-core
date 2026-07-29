namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueScoresByWeek;

public class GetLeagueScoresByWeekQuery
{
    public required Guid LeagueId { get; init; }

    /// <summary>The caller — must be a member of the league.</summary>
    public required Guid UserId { get; init; }
}
