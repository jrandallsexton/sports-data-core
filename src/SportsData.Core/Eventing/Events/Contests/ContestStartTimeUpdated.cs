using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests
{
    public record ContestStartTimeUpdated(
        Guid ContestId,
        DateTime NewStartTime,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
