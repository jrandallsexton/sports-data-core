using SportsData.Core.Models.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Venues
{
    public class VenueCreated(
        VenueCanonicalModel venue,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public VenueCanonicalModel Canonical { get; init; } = venue;
    }
}
