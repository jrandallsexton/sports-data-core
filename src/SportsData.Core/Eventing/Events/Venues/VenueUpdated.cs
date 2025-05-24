using SportsData.Core.Models.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Venues
{
    public class VenueUpdated(
        VenueDto venue,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public VenueDto Canonical { get; init; } = venue;
    }
}
