using System;

using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Eventing.Events.Venues;

public record VenueCreated(
    VenueDto Canonical,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(CorrelationId, CausationId);