using SportsData.Core.Dtos.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Franchise
{
    public class FranchiseSeasonRecordCreated(
        FranchiseSeasonRecordDto dto,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public FranchiseSeasonRecordDto Canonical { get; init; } = dto;
    }
}
