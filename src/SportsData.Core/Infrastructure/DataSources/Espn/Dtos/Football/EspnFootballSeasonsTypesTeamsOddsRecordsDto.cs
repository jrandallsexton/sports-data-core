#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Common.Routing;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballSeasonsTypesTeamsOddsRecordsDto : IHasRoutingKey
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("pageIndex")]
        public int PageIndex { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("pageCount")]
        public int PageCount { get; set; }

        [JsonPropertyName("items")]
        public List<OddsRecordItem> Items { get; set; }

        public string RoutingKey { get; } = "espn.v2.sports.football.leagues.college-football.seasons.types.teams.odds-records";

        public class OddsRecordItem
        {
            [JsonPropertyName("abbreviation")]
            public string Abbreviation { get; set; }

            [JsonPropertyName("displayName")]
            public string DisplayName { get; set; }

            [JsonPropertyName("shortDisplayName")]
            public string ShortDisplayName { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("stats")]
            public List<OddsRecordStat> Stats { get; set; }
        }

        public class OddsRecordStat
        {
            [JsonPropertyName("displayName")]
            public string DisplayName { get; set; }

            [JsonPropertyName("abbreviation")]
            public string Abbreviation { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("value")]
            public double Value { get; set; }

            [JsonPropertyName("displayValue")]
            public string DisplayValue { get; set; }
        }
    }
}