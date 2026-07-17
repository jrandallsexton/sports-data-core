namespace SportsData.Api.Application.UI.Leagues.Commands.CloneLeague;

public sealed class CloneLeagueCommand
{
    public required Guid UserId { get; init; }

    public required Guid SourceLeagueId { get; init; }

    /// <summary>Name for the clone (FE pre-fills "&lt;Original&gt; (Copy)").</summary>
    public required string Name { get; init; }

    /// <summary>When true, invite the source league's members to the clone.</summary>
    public bool InviteMembers { get; init; }
}
