using SportsData.Core.Common;

namespace SportsData.Producer.Application.Contests
{
    public record UpdateContestCommand(
        Guid ContestId,
        int SeasonYear,
        SourceDataProvider SourceDataProvider,
        Sport Sport,
        Guid CorrelationId);
}
