using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

#pragma warning disable CS8618
public class EspnFootballSeasonsTypesDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("items")]
    public List<EspnFootballSeasonTypeDto> Items { get; set; }
}