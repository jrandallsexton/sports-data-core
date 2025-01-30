using SportsData.Core.Models.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Athletes
{
    public class AthleteCreated(
        AthleteCanonicalModel athlete,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public AthleteCanonicalModel Canonical { get; init; } = athlete;
    }
}
