using System;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Eventing.Events.Conferences
{
    public class ConferenceSeasonCreated(
        ConferenceSeasonDto conferenceSeason,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public ConferenceSeasonDto Canonical { get; init; } = conferenceSeason;
    }
}
