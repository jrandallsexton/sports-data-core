using System.Text.Json.Serialization;

using SportsData.Core.Converters;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnAddressDto
    {
        [JsonPropertyName("city")]
        public string City { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("zipCode")]
        [JsonConverter(typeof(ParseStringToLongConverter))]
        public long ZipCode { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; } = "USA";
    }
}