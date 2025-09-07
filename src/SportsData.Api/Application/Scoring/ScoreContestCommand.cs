namespace SportsData.Api.Application.Scoring;

public class ScoreContestCommand
{
    public Guid ContestId { get; set; }
    public Guid CorrelationId { get; set; }

    public ScoreContestCommand()
    {
        CorrelationId = Guid.NewGuid();
    }

    public ScoreContestCommand(Guid contestId)
        : this()
    {
        ContestId = contestId;
    }

    public ScoreContestCommand(Guid contestId, Guid correlationId)
    {
        ContestId = contestId;
        CorrelationId = correlationId;
    }
}