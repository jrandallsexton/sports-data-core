using System;

namespace SportsData.Core.Eventing.Events.Contests
{
    public record CompetitionPlayCompleted(
        Guid CompetitionPlayId,
        Guid CompetitionId,
        Guid ContestId,
        string PlayDescription,
        Guid CorrelationId,
        Guid CausationId) : EventBase(CorrelationId, CausationId);
}
