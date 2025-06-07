using SportsData.Core.Dtos.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Franchise
{
    public class FranchiseUpdated(
        FranchiseDto franchise,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public FranchiseDto Canonical { get; init; } = franchise;
    }
}
