using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Franchise;

public record FranchiseUpdated(
    FranchiseDto Canonical,
    Uri? Ref,
    Sport Sport,
    int? SeasonYear,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);