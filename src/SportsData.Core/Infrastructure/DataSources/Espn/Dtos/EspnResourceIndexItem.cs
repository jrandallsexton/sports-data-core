using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

public class EspnResourceIndexItem : IHasRef
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; }

    public int Id { get; set; }
}