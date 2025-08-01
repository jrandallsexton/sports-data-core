using System;

namespace SportsData.Core.Eventing.Events.Contests
{
    public record CompetitionStatusChanged(
        Guid CompetitionId,
        string Status,
        Guid CorrelationId,
        Guid CausationId) : EventBase(CorrelationId, CausationId);
}
