using SportsData.Core.Common.Routing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballSeasonsTypesWeeksRankingsDto : IHasRoutingKey
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("items")]
        public List<EspnLinkDto> Items { get; set; }

        public string RoutingKey { get; } = "espn.v2.sports.football.leagues.college-football.seasons.types.weeks.rankings";
    }
}