using Newtonsoft.Json;

namespace SportsData.Provider.Infrastructure.Providers.Espn.DTOs.ResourceIndex;

public class Item
{
    [JsonProperty("$ref")]
    public string href { get; set; }
    public int id { get; set; }
}