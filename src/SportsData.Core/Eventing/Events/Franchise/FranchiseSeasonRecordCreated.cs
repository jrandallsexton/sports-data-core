using SportsData.Core.Dtos.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Franchise;

public record FranchiseSeasonRecordCreated(
    FranchiseSeasonRecordDto Canonical,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(CorrelationId, CausationId);