namespace SportsData.Api.Application.UI.Leagues.Commands.DeclineLeagueInvitation;

public class DeclineLeagueInvitationCommand
{
    public Guid InvitationId { get; init; }

    public Guid UserId { get; init; }
}
