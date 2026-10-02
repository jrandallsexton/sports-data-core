using SportsData.Core.Common;

namespace SportsData.Producer.Application.Contests.Commands.UpdateContest
{
    public record UpdateContestCommand(
        Guid ContestId,
        SourceDataProvider SourceDataProvider,
        Sport Sport,
        Guid CorrelationId);
}
