using System;

namespace SportsData.Core.Eventing.Events.Franchise
{
    public record FranchiseSeasonEnrichmentCompleted(
        Guid FranchiseSeasonId,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(CorrelationId, CausationId);
}
