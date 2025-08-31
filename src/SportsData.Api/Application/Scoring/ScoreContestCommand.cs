namespace SportsData.Api.Application.Scoring;

public class ScoreContestCommand
{
    public Guid ContestId { get; set; }

    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}