namespace SportsData.Api.Application.UI.Leagues.Commands.AddMatchup;

public class AddMatchupCommand
{
    public required Guid LeagueId { get; init; }

    public required Guid ContestId { get; init; }

    public required Guid UserId { get; init; }

    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
