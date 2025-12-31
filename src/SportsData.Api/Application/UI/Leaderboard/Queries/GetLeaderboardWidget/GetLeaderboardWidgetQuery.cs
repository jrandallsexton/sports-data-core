namespace SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboardWidget;

public class GetLeaderboardWidgetQuery
{
    public required Guid UserId { get; init; }

    /// <summary>
    /// The season year to get leaderboard widget for. Defaults to current year if not specified.
    /// </summary>
    public int? SeasonYear { get; init; }
}
