using SportsData.Core.Dtos.Canonical;

using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Athletes
{
    public record AthletePositionCreated(
        AthletePositionDto Canonical,
        Uri? Ref,
        Sport Sport,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(Ref, Sport, null, CorrelationId, CausationId);
}