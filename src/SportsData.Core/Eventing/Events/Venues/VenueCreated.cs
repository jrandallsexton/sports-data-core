using SportsData.Core.Models.Canonical;

namespace SportsData.Core.Eventing.Events.Venues
{
    public class VenueCreated : EventBase
    {
        public string Id { get; set; }

        public VenueCanonicalModel Canonical { get; set; }
    }
}
