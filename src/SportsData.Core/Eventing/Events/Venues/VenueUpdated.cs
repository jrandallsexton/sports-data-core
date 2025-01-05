namespace SportsData.Core.Eventing.Events.Venues
{
    public class VenueUpdated : EventBase
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}
