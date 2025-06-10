using System;

using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Eventing.Events.Positions;

public record PositionCreated(
    PositionDto Canonical,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(CorrelationId, CausationId);