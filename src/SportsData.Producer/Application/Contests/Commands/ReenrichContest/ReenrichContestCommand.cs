namespace SportsData.Producer.Application.Contests.Commands.ReenrichContest;

public record ReenrichContestCommand
{
    public Guid ContestId { get; init; }

    public Guid CorrelationId { get; init; } = Guid.Empty;
}
