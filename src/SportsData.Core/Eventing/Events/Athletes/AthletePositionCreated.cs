using SportsData.Core.Dtos.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Athletes
{
    public class AthletePositionCreated(
        AthletePositionDto dto,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public AthletePositionDto Canonical { get; init; } = dto;
    }
}
