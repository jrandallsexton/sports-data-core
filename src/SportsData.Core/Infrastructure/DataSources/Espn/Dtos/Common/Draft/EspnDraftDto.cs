using System;
using System.Text.Json.Serialization;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common.Draft;

public class EspnDraftDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; } = null!;

    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("numberOfRounds")]
    public int NumberOfRounds { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("shortDisplayName")]
    public string? ShortDisplayName { get; set; }

    [JsonPropertyName("rounds")]
    public EspnLinkDto? Rounds { get; set; }

    [JsonPropertyName("athletes")]
    public EspnLinkDto? Athletes { get; set; }

    [JsonPropertyName("status")]
    public EspnLinkDto? Status { get; set; }
}
