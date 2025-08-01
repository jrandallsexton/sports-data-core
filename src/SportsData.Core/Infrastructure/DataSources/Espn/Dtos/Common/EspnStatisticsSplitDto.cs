#pragma warning disable CS8618

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

public class EspnStatisticsSplitDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("categories")]
    public List<EspnStatisticsCategoryDto> Categories { get; set; }
}