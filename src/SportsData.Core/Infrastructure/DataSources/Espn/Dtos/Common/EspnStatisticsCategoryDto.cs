using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618

public class EspnStatisticsCategoryDto 
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    [JsonPropertyName("shortDisplayName")]
    public string ShortDisplayName { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; }

    [JsonPropertyName("stats")]
    public List<EspnStatisticsStatDto> Stats { get; set; }
}