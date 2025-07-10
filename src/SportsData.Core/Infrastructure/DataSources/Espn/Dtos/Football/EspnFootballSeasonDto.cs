#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballSeasonDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("startDate")]
        public string StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string EndDate { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("type")]
        public EspnFootballSeasonTypeDto Type { get; set; }

        [JsonPropertyName("types")]
        public EspnFootballSeasonsTypesDto Types { get; set; }

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
}
