using SportsData.Core.Common.Routing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballPositionDto : IHasRoutingKey
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("leaf")]
        public bool Leaf { get; set; }

        [JsonPropertyName("parent")]
        public EspnLinkDto Parent { get; set; }

        public string RoutingKey { get; } = "espn.v2.sports.football.leagues.college-football.positions";
    }
}