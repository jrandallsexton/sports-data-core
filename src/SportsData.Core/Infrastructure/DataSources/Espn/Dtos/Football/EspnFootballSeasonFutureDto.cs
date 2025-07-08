#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

public class EspnFootballSeasonFutureDto
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("futures")]
    public List<EspnFootballSeasonFutureItemDto> Futures { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

public class EspnFootballSeasonFutureItemDto
{
    [JsonPropertyName("provider")]
    public EspnFootballSeasonFutureProviderDto Provider { get; set; }

    [JsonPropertyName("books")]
    public List<EspnFootballSeasonFutureBookDto> Books { get; set; }
}

public class EspnFootballSeasonFutureProviderDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("active")]
    public int Active { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}

public class EspnFootballSeasonFutureBookDto
{
    [JsonPropertyName("team")]
    public EspnLinkDto? Team { get; set; }

    [JsonPropertyName("athlete")]
    public EspnLinkDto? Athlete { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; }
}
