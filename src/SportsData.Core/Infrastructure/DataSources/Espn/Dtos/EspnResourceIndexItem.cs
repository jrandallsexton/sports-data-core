using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

public class EspnResourceIndexItem
{
    [JsonPropertyName("$ref")]
    public string Href { get; set; }

    public int Id { get; set; }
}