using System;

namespace SportsData.Core.Eventing.Events.Contests
{
    // TODO: Implement a DTO for the ContestOdds canonical representation
    public record ContestOddsCreated(
        Guid ContestId,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(CorrelationId, CausationId);
}
