namespace SportsData.Api.Application.UI.Leagues.Commands.DeleteLeague;

public class DeleteLeagueCommand
{
    public required Guid UserId { get; init; }
    public required Guid LeagueId { get; init; }
}
