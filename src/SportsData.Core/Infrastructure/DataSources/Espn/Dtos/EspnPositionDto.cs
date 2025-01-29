using Newtonsoft.Json;

using SportsData.Core.Converters;

using System;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnPositionDto
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }

        [JsonProperty("id")]
        [System.Text.Json.Serialization.JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonProperty("leaf")]
        public bool Leaf { get; set; }
    }
}
