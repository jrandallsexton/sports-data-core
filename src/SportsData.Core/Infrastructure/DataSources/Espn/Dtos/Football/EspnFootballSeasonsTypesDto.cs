using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

#pragma warning disable CS8618
public class EspnFootballSeasonsTypesDto
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("items")]
    public List<EspnFootballSeasonTypeDto> Items { get; set; }
}