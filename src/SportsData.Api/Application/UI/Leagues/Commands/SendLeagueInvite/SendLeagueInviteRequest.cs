namespace SportsData.Api.Application.UI.Leagues.Commands.SendLeagueInvite;

public class SendLeagueInviteRequest
{
    public Guid LeagueId { get; set; }

    public string Email { get; set; } = default!;

    public string? InviteeName { get; set; }
}