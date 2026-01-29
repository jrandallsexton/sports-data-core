using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

public class EspnEventCompetitionCompetitorRosterDto
{
    [JsonPropertyName("$ref")]
    public Uri? Ref { get; set; }

    [JsonPropertyName("entries")]
    public List<EspnRosterEntryDto> Entries { get; set; } = new();

    [JsonPropertyName("competition")]
    public EspnLinkDto? Competition { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto? Team { get; set; }
}

public class EspnRosterEntryDto
{
    [JsonPropertyName("playerId")]
    public int PlayerId { get; set; }

    [JsonPropertyName("period")]
    public int Period { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("starter")]
    public bool Starter { get; set; }

    [JsonPropertyName("jersey")]
    public string? Jersey { get; set; }

    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("athlete")]
    public EspnLinkDto? Athlete { get; set; }

    [JsonPropertyName("position")]
    public EspnLinkDto? Position { get; set; }

    [JsonPropertyName("statistics")]
    public EspnLinkDto? Statistics { get; set; }

    [JsonPropertyName("didNotPlay")]
    public bool DidNotPlay { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}
