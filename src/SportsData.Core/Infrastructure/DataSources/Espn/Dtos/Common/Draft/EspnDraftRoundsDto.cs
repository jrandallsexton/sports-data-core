using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common.Draft;

public class EspnDraftRoundsDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; } = null!;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("items")]
    public List<EspnDraftRoundDto> Items { get; set; } = new();
}

public class EspnDraftRoundDto
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("shortDisplayName")]
    public string? ShortDisplayName { get; set; }

    [JsonPropertyName("picks")]
    public List<EspnDraftPickDto> Picks { get; set; } = new();
}

public class EspnDraftPickDto
{
    [JsonPropertyName("pick")]
    public int Pick { get; set; }

    [JsonPropertyName("overall")]
    public int Overall { get; set; }

    [JsonPropertyName("round")]
    public int Round { get; set; }

    [JsonPropertyName("traded")]
    public bool Traded { get; set; }

    [JsonPropertyName("tradeNote")]
    public string? TradeNote { get; set; }

    [JsonPropertyName("athlete")]
    public EspnLinkDto? Athlete { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto? Team { get; set; }

    [JsonPropertyName("status")]
    public EspnDraftPickStatusDto? Status { get; set; }
}

public class EspnDraftPickStatusDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
