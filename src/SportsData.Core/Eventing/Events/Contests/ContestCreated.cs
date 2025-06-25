using SportsData.Core.Dtos.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Contests
{
    public record ContestCreated(
        ContestDto Canonical,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(CorrelationId, CausationId);
}
