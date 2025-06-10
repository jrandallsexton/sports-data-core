using System;

namespace SportsData.Core.Eventing
{
    public abstract record EventBase(Guid CorrelationId, Guid CausationId)
    {
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    }
}