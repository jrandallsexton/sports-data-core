using SportsData.Core.Models.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Franchise
{
    public class FranchiseCreated(
        FranchiseCanonicalModel franchise,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public FranchiseCanonicalModel Canonical { get; init; } = franchise;
    }
}
