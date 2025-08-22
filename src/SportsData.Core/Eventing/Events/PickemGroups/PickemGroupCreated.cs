using System;

namespace SportsData.Core.Eventing.Events.PickemGroups
{
    public record PickemGroupCreated(
        Guid GroupId,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(CorrelationId, CausationId);
}
