using System;

namespace SportsData.Core.Eventing.Events.Contests;

public record ContestOddsUpdated(
    Guid ContestId,
    string Message,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(CorrelationId, CausationId);