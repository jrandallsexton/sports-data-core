using SportsData.Core.Models.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Conferences
{
    public class ConferenceCreated(
        ConferenceCanonicalModel conference,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public ConferenceCanonicalModel Canonical { get; init; } = conference;
    }
}
