namespace SportsData.Producer.Application.Competitions;

public class StreamFootballCompetitionCommand
{
    public Guid ContestId { get; set; }
    public Guid CompetitionId { get; set; }
    public Guid CorrelationId { get; init; }
}