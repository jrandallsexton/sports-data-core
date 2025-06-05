using SportsData.Core.Common.Routing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballAwardDto : IHasRoutingKey
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("history")]
        public string History { get; set; }

        [JsonPropertyName("season")]
        public EspnLinkDto Season { get; set; }

        [JsonPropertyName("winners")]
        public List<WinnerDto> Winners { get; set; }

        [JsonPropertyName("links")]
        public List<EspnLinkFullDto> Links { get; set; }

        public string RoutingKey { get; } = "espn.v2.sports.football.leagues.college-football.award";

        public class WinnerDto
        {
            [JsonPropertyName("athlete")]
            public EspnLinkDto Athlete { get; set; }

            [JsonPropertyName("team")]
            public EspnLinkDto Team { get; set; }
        }
    }
}