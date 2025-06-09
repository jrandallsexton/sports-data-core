#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Common.Routing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballCalendarBlacklistDto : IHasRoutingKey
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("eventDate")]
        public EventDateDto EventDate { get; set; }

        [JsonPropertyName("sections")]
        public List<SectionDto> Sections { get; set; }

        [JsonPropertyName("season")]
        public EspnLinkDto Season { get; set; }

        public string RoutingKey { get; } = "espn.v2.sports.football.leagues.college-football.calendar.blacklist";

        public class EventDateDto
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("dates")]
            public List<DateTime> Dates { get; set; }
        }

        public class SectionDto
        {
            [JsonPropertyName("label")]
            public string Label { get; set; }

            [JsonPropertyName("value")]
            public string Value { get; set; }

            [JsonPropertyName("startDate")]
            public DateTime StartDate { get; set; }

            [JsonPropertyName("endDate")]
            public DateTime EndDate { get; set; }

            [JsonPropertyName("entries")]
            public List<EntryDto> Entries { get; set; }

            [JsonPropertyName("seasonType")]
            public EspnLinkDto SeasonType { get; set; }
        }

        public class EntryDto
        {
            [JsonPropertyName("label")]
            public string Label { get; set; }

            [JsonPropertyName("alternateLabel")]
            public string AlternateLabel { get; set; }

            [JsonPropertyName("detail")]
            public string Detail { get; set; }

            [JsonPropertyName("value")]
            public string Value { get; set; }

            [JsonPropertyName("startDate")]
            public DateTime StartDate { get; set; }

            [JsonPropertyName("endDate")]
            public DateTime EndDate { get; set; }
        }
    }
}