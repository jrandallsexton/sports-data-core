using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Franchise
{
    public record FranchiseSeasonEnrichmentCompleted(
        Guid FranchiseSeasonId,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
