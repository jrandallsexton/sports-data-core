using System;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Eventing.Events.Franchise
{
    public class FranchiseSeasonCreated(
        FranchiseSeasonDto franchiseSeason,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public FranchiseSeasonDto Canonical { get; init; } = franchiseSeason;
    }
}
