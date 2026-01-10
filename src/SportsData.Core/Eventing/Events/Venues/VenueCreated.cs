using System;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Eventing.Events.Venues;

public record VenueCreated(
    VenueDto Canonical,
    Uri Ref,
    Sport Sport,
    int? SeasonYear,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);