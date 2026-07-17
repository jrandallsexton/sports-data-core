namespace SportsData.Api.Application.UI.Leagues.Commands.CloneLeague;

/// <summary>
/// Body for POST /ui/leagues/{id}/clone. The source league is the route id.
/// </summary>
public sealed class CloneLeagueRequest
{
    public string Name { get; set; } = string.Empty;

    public bool InviteMembers { get; set; }
}
