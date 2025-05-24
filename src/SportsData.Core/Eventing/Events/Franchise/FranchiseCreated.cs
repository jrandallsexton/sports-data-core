using System;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Eventing.Events.Franchise
{
    public class FranchiseCreated(
        FranchiseDto franchise,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public FranchiseDto Canonical { get; init; } = franchise;
    }
}
