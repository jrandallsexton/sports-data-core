using System;

namespace SportsData.Core.Eventing.Events.Contests
{
    public record ContestEnrichmentCompleted(
        Guid ContestId,
        Guid CorrelationId,
        Guid CausationId
        ) : EventBase(CorrelationId, CausationId);
}