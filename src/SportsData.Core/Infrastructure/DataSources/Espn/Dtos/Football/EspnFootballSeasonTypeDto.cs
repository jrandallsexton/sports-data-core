#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Common.Routing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballSeasonTypeDto : IHasRoutingKey
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("hasGroups")]
        public bool HasGroups { get; set; }

        [JsonPropertyName("hasStandings")]
        public bool HasStandings { get; set; }

        [JsonPropertyName("hasLegs")]
        public bool HasLegs { get; set; }

        [JsonPropertyName("groups")]
        public EspnLinkDto Groups { get; set; }

        [JsonPropertyName("weeks")]
        public EspnLinkDto Weeks { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        public string RoutingKey { get; } = "espn.v2.sports.football.leagues.college-football.season-type";
    }
}