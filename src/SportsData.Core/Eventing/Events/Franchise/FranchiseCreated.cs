namespace SportsData.Core.Eventing.Events.Franchise
{
    public class FranchiseCreated : EventBase
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}
