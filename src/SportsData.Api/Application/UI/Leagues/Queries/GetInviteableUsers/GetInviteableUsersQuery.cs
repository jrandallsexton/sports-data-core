namespace SportsData.Api.Application.UI.Leagues.Queries.GetInviteableUsers;

public class GetInviteableUsersQuery
{
    public required Guid LeagueId { get; init; }

    /// <summary>The user doing the searching — excluded from results.</summary>
    public required Guid RequestingUserId { get; init; }

    /// <summary>Search term matched against username and display name.</summary>
    public string? Q { get; init; }
}
