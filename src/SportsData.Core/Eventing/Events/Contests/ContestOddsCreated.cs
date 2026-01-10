using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests
{
    // TODO: Implement a DTO for the ContestOdds canonical representation
    public record ContestOddsCreated(
        Guid ContestId,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
