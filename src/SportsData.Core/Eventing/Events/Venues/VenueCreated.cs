namespace SportsData.Core.Eventing.Events.Venues
{
    public class VenueCreated : EventBase
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}
