using System;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Eventing.Events.Athletes
{
    public class AthleteCreated(
        AthleteDto athlete,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public AthleteDto Canonical { get; init; } = athlete;
    }
}
