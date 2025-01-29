using SportsData.Core.Models.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Positions
{
    public class PositionCreated(
        PositionCanonicalModel model,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public PositionCanonicalModel Canonical { get; set; }
    }
}
