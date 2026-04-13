#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Collections.Generic;
using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;

public class EspnBaseballEventCompetitionCompetitorDto : EspnEventCompetitionCompetitorDto
{
    [JsonPropertyName("probables")]
    public List<EspnBaseballProbableDto>? Probables { get; set; }
}

public class EspnBaseballProbableDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("shortDisplayName")]
    public string? ShortDisplayName { get; set; }

    [JsonPropertyName("abbreviation")]
    public string? Abbreviation { get; set; }

    [JsonPropertyName("playerId")]
    public int PlayerId { get; set; }

    [JsonPropertyName("athlete")]
    public EspnLinkDto? Athlete { get; set; }

    [JsonPropertyName("statistics")]
    public EspnLinkDto? Statistics { get; set; }
}
