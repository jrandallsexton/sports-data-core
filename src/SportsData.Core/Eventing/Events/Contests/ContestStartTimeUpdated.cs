using System;

namespace SportsData.Core.Eventing.Events.Contests
{
    public record ContestStartTimeUpdated(
        Guid ContestId,
        DateTime NewStartTime,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(CorrelationId, CausationId);
}
