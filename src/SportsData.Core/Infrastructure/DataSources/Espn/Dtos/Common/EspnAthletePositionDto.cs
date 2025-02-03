using Newtonsoft.Json;

using System;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnAthletePositionDto
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonProperty("leaf")]
        public bool Leaf { get; set; }

        [JsonProperty("parent")]
        public EspnLinkDto Parent { get; set; }
    }
}
