using Newtonsoft.Json;
using SportsData.Core.Converters;

namespace SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Venue;

public class Address
{
    [JsonProperty("city")]
    public string City { get; set; }

    [JsonProperty("state")]
    public string State { get; set; }

    [JsonProperty("zipCode")]
    [System.Text.Json.Serialization.JsonConverter(typeof(ParseStringConverter))]
    public long ZipCode { get; set; }
}