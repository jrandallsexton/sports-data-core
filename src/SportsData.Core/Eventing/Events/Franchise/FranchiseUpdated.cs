using SportsData.Core.Dtos.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Franchise;

public record FranchiseUpdated(
    FranchiseDto Canonical,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(CorrelationId, CausationId);