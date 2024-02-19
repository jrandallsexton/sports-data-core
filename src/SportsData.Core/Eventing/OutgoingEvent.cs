using System;

namespace SportsData.Core.Eventing
{
    public class OutgoingEvent : EventingBase
    {
        public bool Raised { get; set; }
        public DateTime? RaisedUtc { get; set; }
    }
}
