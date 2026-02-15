using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing
{
    public abstract record EventBase(Uri? Ref, Sport Sport, int? SeasonYear, Guid CorrelationId, Guid CausationId)
    {
        public Guid MessageId { get; init; } = Guid.NewGuid();
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    }
}