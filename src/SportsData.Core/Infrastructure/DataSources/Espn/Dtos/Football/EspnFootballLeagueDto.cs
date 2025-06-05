using SportsData.Core.Common.Routing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballLeagueDto : IHasRoutingKey
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("season")]
        public EspnLinkDto Season { get; set; }

        [JsonPropertyName("seasons")]
        public EspnLinkDto Seasons { get; set; }

        [JsonPropertyName("groups")]
        public EspnLinkDto Groups { get; set; }

        [JsonPropertyName("teams")]
        public EspnLinkDto Teams { get; set; }

        [JsonPropertyName("events")]
        public EspnLinkDto Events { get; set; }

        [JsonPropertyName("links")]
        public List<EspnLinkDto> Links { get; set; }

        public string RoutingKey { get; } = "espn.v2.sports.football.leagues.college-football";
    }
}