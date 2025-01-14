using Newtonsoft.Json;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

public class EspnResourceIndexItem
{
    [JsonProperty("$ref")]
    public string href { get; set; }
    public int id { get; set; }
}