using SportsData.Core.Models.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Franchise
{
    public class FranchiseSeasonCreated(
        FranchiseSeasonCanonicalModel franchiseSeason,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public FranchiseSeasonCanonicalModel Canonical { get; init; } = franchiseSeason;
    }
}
