#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Text.Json.Serialization;

using SportsData.Core.Converters;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnAthleteStatusDto
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(ParseStringToLongConverter))]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }
    }
}