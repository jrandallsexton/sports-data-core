namespace SportsData.Api.Application.UI.Leagues.Commands.SendLeagueInvite;

public class SendLeagueInviteCommand
{
    public required Guid LeagueId { get; init; }
    public required string Email { get; init; }
    public string? InviteeName { get; init; }
}
