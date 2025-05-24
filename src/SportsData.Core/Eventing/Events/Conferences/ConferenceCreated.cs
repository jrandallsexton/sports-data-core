using System;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Eventing.Events.Conferences
{
    public class ConferenceCreated(
        ConferenceDto conference,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public ConferenceDto Canonical { get; init; } = conference;
    }
}
