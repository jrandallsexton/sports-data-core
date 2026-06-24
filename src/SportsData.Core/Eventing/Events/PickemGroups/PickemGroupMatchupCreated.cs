using SportsData.Core.Common;

using System;

namespace SportsData.Core.Eventing.Events.PickemGroups
{
    public record PickemGroupMatchupCreated(
        Guid GroupId,
        Guid ContestId,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);
}
