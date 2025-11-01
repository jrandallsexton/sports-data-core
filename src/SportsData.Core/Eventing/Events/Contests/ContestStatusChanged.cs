using System;

namespace SportsData.Core.Eventing.Events.Contests
{
    public record ContestStatusChanged(
        Guid ContestId,
        string Status,
        Guid CorrelationId,
        Guid CausationId) : EventBase(CorrelationId, CausationId);
}
