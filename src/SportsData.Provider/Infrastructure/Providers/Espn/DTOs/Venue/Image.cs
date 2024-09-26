using Newtonsoft.Json;

namespace SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Venue;

public class Image
{
    [JsonProperty("href")]
    public Uri Href { get; set; }

    [JsonProperty("width")]
    public long Width { get; set; }

    [JsonProperty("height")]
    public long Height { get; set; }

    [JsonProperty("alt")]
    public string Alt { get; set; }

    [JsonProperty("rel")]
    public List<string> Rel { get; set; }
}