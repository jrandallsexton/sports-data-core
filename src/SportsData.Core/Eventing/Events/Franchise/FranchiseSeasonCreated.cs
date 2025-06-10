using System;

using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Eventing.Events.Franchise;

public record FranchiseSeasonCreated(
    FranchiseSeasonDto Canonical,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(CorrelationId, CausationId);