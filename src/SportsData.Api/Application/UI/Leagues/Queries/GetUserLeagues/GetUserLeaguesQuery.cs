namespace SportsData.Api.Application.UI.Leagues.Queries.GetUserLeagues;

public class GetUserLeaguesQuery
{
    public required Guid UserId { get; init; }

    /// <summary>
    /// When true, deactivated (past-season) leagues are returned alongside
    /// active ones, carrying a non-null DeactivatedUtc so the caller can tell
    /// them apart. Defaults to false to keep the historical contract: callers
    /// that just want the user's live leagues need no change.
    /// </summary>
    public bool IncludeDeactivated { get; init; }
}
