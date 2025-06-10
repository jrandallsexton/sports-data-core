namespace SportsData.Core.Eventing.Events.Conferences
{
    public class ConferenceUpdated
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
    }
}
