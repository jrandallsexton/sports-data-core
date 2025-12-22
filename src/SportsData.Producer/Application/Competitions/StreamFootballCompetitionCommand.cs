using SportsData.Core.Common;

namespace SportsData.Producer.Application.Competitions;

public class StreamFootballCompetitionCommand
{
    public Sport Sport { get; set; }

    public int SeasonYear { get; set; }

    public SourceDataProvider DataProvider { get; set; }

    public Guid ContestId { get; set; }

    public Guid CompetitionId { get; set; }

    public Guid CorrelationId { get; init; }
}