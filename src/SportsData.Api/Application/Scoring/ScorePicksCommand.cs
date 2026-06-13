namespace SportsData.Api.Application.Scoring;

public class ScorePicksCommand
{
    public Guid ContestId { get; set; }
    public Guid CorrelationId { get; set; }

    public ScorePicksCommand()
    {
        CorrelationId = Guid.NewGuid();
    }

    public ScorePicksCommand(Guid contestId)
        : this()
    {
        ContestId = contestId;
    }

    public ScorePicksCommand(Guid contestId, Guid correlationId)
    {
        ContestId = contestId;
        CorrelationId = correlationId;
    }
}