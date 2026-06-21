namespace SportsData.Producer.Application.Contests.Commands;

public record ReenrichContestCommand
{
    public Guid ContestId { get; init; }

    public Guid CorrelationId { get; init; } = Guid.Empty;
}
