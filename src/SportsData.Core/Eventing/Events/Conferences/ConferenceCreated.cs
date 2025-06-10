using System;

using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Eventing.Events.Conferences
{
    public record ConferenceCreated(
        ConferenceDto Canonical,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(CorrelationId, CausationId);
}