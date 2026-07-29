namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueById;

public class GetLeagueByIdQuery
{
    public required Guid LeagueId { get; init; }

    /// <summary>
    /// The caller. Drives the tiered response: members receive the full roster,
    /// non-members receive settings + member count only.
    /// See docs/audit/league-authorization-idor.md.
    /// </summary>
    public required Guid UserId { get; init; }
}
