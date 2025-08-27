using System;

namespace SportsData.Core.Eventing.Events.Previews
{
    public record PreviewGenerated(
        Guid ContestId,
        string Message,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(CorrelationId, CausationId);
}
