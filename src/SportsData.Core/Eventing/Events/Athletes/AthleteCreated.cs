using System;

using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Eventing.Events.Athletes
{
    public record AthleteCreated(
        AthleteDto Canonical,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(CorrelationId, CausationId);
}