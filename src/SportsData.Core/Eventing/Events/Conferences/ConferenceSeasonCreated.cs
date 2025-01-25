using SportsData.Core.Models.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Conferences
{
    public class ConferenceSeasonCreated(
        ConferenceSeasonCanonicalModel conferenceSeason,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public ConferenceSeasonCanonicalModel Canonical { get; init; } = conferenceSeason;
    }
}
