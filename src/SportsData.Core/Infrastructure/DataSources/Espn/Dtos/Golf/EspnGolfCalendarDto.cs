using Newtonsoft.Json;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Golf
{

    public class EspnGolfCalendarDto
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }

        public string Type { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public EventDate EventDate { get; set; }

        [JsonProperty("sections")]
        public List<EspGolfCalendarEventDto> Events { get; set; }

        public EspnLinkDto Season { get; set; }
    }
    
    public class Event
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }
    }

    public class EventDate
    {
        [JsonProperty("type")]
        public string EventDateType { get; set; }

        public List<DateTime> Dates { get; set; }
    }

    public class EspGolfCalendarEventDto
    {
        public string Id { get; set; }

        public string Label { get; set; }

        public string Detail { get; set; }

        public string StartDate { get; set; }

        public string EndDate { get; set; }

        public Event Event { get; set; }
    }
}
