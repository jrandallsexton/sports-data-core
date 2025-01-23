using System;

namespace SportsData.Core.Eventing
{
    public abstract class EventingBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string EventPayload { get; set; }

        public string EventType { get; set; }

        public Guid CorrelationId { get; set; }

        public Guid CausationId { get; set; }

        public DateTime CreatedUtc { get; set; }

        public int CreatedBy { get; set; }

        public DateTime? ModifiedUtc { get; set; }

        public int? ModifiedBy { get; set; }

        public DateTime? LockedUtc { get; set; }
    }

    // TODO: Change to record?
    public abstract class EventBase
    {
        public Guid CorrelationId { get; init; } = Guid.NewGuid();

        public Guid CausationId { get; init; } = Guid.NewGuid();

        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    }
}