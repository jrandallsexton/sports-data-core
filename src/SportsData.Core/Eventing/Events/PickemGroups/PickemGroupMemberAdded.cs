using SportsData.Core.Common;

using System;

namespace SportsData.Core.Eventing.Events.PickemGroups
{
    public record PickemGroupMemberAdded(
        Guid GroupId,
        Guid UserId,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);
}
