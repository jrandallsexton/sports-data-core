namespace SportsData.Api.Application.UI.Leagues.Commands.InviteUserToLeague;

public class InviteUserToLeagueCommand
{
    public required Guid LeagueId { get; init; }
    public required Guid InviteeUserId { get; init; }
    public required Guid InvitedByUserId { get; init; }
}
