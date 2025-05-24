using System;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Eventing.Events.Positions
{
    public class PositionCreated(
        PositionDto model,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public PositionDto Canonical { get; set; }
    }
}
