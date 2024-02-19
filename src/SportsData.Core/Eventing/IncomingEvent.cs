using System;

namespace SportsData.Core.Eventing
{
    public class IncomingEvent : EventingBase
    {
        public bool Handled { get; set; }
        public DateTime? HandledUtc { get; set; }
        public bool Processed { get; set; }
        public DateTime? ProcessedUtc { get; set; }
    }
}
