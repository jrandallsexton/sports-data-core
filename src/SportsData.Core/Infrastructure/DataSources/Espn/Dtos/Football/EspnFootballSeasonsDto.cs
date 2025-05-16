using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballSeasonsDto
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("startDate")]
        public string StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string EndDate { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("type")]
        public EspnFootballSeasonsType Type { get; set; }

        [JsonPropertyName("types")]
        public EspnFootballSeasonsTypes Types { get; set; }

        [JsonPropertyName("rankings")]
        public EspnResourceIndexItem Rankings { get; set; }

        [JsonPropertyName("athletes")]
        public EspnResourceIndexItem Athletes { get; set; }

        [JsonPropertyName("awards")]
        public EspnResourceIndexItem Awards { get; set; }

        [JsonPropertyName("futures")]
        public EspnResourceIndexItem Futures { get; set; }

        [JsonPropertyName("leaders")]
        public EspnResourceIndexItem Leaders { get; set; }
    }

    public class EspnFootballSeasonsType
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
        public string StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string EndDate { get; set; }

        [JsonPropertyName("hasGroups")]
        public bool HasGroups { get; set; }

        [JsonPropertyName("hasStandings")]
        public bool HasStandings { get; set; }

        [JsonPropertyName("hasLegs")]
        public bool HasLegs { get; set; }

        [JsonPropertyName("groups")]
        public EspnResourceIndexItem Groups { get; set; }

        [JsonPropertyName("weeks")]
        public EspnResourceIndexItem Weeks { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("leaders")]
        public EspnResourceIndexItem Leaders { get; set; }
    }

    public class EspnFootballSeasonsTypes
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("items")]
        public List<EspnFootballSeasonsType> Items { get; set; }
    }
}
