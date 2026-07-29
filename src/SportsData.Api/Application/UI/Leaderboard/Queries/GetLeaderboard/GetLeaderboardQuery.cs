namespace SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboard;

public class GetLeaderboardQuery
{
    public required Guid GroupId { get; init; }

    /// <summary>The caller — must be a member of the group.</summary>
    public required Guid UserId { get; init; }
}
