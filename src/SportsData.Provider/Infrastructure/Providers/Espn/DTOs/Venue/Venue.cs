using Newtonsoft.Json;
using SportsData.Core.Converters;

namespace SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Venue
{
    public class Venue
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }

        [JsonProperty("id")]
        [System.Text.Json.Serialization.JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("fullName")]
        public string FullName { get; set; }

        [JsonProperty("shortName")]
        public string ShortName { get; set; }

        [JsonProperty("address")]
        public Address Address { get; set; }

        [JsonProperty("capacity")]
        public long Capacity { get; set; }

        [JsonProperty("grass")]
        public bool Grass { get; set; }

        [JsonProperty("indoor")]
        public bool Indoor { get; set; }

        [JsonProperty("images")]
        public List<Image> Images { get; set; }
    }
}
