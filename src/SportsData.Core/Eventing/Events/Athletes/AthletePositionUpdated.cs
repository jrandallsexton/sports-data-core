using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Athletes
{
    public record AthletePositionUpdated(
        AthletePositionDto Canonical,
        Uri? Ref,
        Sport Sport,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(Ref, Sport, null, CorrelationId, CausationId);
}