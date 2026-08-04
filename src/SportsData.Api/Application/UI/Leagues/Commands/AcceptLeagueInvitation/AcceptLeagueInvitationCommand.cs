namespace SportsData.Api.Application.UI.Leagues.Commands.AcceptLeagueInvitation;

public class AcceptLeagueInvitationCommand
{
    public Guid InvitationId { get; init; }

    public Guid UserId { get; init; }
}
