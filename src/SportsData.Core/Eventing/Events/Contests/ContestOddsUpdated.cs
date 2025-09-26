using System;

namespace SportsData.Core.Eventing.Events.Contests;

public record ContestOddsUpdated(
    Guid ContestId,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(CorrelationId, CausationId);