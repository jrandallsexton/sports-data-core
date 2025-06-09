#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Golf
{
    public class EspnGolfCalendarDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("eventDate")]
        public EventDate EventDate { get; set; }

        [JsonPropertyName("sections")]
        public List<EspGolfCalendarEventDto> Events { get; set; }

        [JsonPropertyName("season")]
        public EspnLinkDto Season { get; set; }
    }

    public class EventDate
    {
        [JsonPropertyName("type")]
        public string EventDateType { get; set; }

        [JsonPropertyName("dates")]
        public List<DateTime> Dates { get; set; }
    }

    public class EspGolfCalendarEventDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("detail")]
        public string Detail { get; set; }

        [JsonPropertyName("startDate")]
        public string StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string EndDate { get; set; }

        [JsonPropertyName("event")]
        public EventReference Event { get; set; }
    }

    public class EventReference
    {
        // $ref removed
    }
}