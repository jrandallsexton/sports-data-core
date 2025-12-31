using System;

namespace SportsData.Core.Eventing.Events.PickemGroups;

public record PickemGroupMatchupAdded(
    Guid GroupId,
    Guid ContestId,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(CorrelationId, CausationId);
