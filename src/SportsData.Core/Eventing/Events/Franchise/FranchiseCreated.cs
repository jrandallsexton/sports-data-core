using SportsData.Core.Models.Canonical;

namespace SportsData.Core.Eventing.Events.Franchise
{
    public class FranchiseCreated(FranchiseCanonicalModel franchise) : EventBase
    {
        public FranchiseCanonicalModel Franchise { get; init; } = franchise;
    }
}
