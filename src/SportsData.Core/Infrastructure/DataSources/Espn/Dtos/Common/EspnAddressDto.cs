using Newtonsoft.Json;

using SportsData.Core.Converters;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnAddressDto
    {
        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("zipCode")]
        [System.Text.Json.Serialization.JsonConverter(typeof(ParseStringConverter))]
        public long ZipCode { get; set; }
    }
}
