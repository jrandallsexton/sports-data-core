using System;
using System.Text.Json.Serialization;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618

/// <summary>
/// Represents an ESPN team season injury record accessed via TeamSeason.Injuries link.
/// ESPN uses the "injuries" endpoint for various player notes including transfers, injuries, general announcements, etc.
/// </summary>
public class EspnTeamSeasonInjuryDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; } = default!;

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public EspnTeamSeasonInjuryTypeDto? Type { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    // Notes format uses "headline"
    [JsonPropertyName("headline")]
    public string? Headline { get; set; }

    // Injuries format uses "shortComment"
    [JsonPropertyName("shortComment")]
    public string? ShortComment { get; set; }

    // Notes format uses "text"
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    // Injuries format uses "longComment"
    [JsonPropertyName("longComment")]
    public string? LongComment { get; set; }

    [JsonPropertyName("source")]
    public EspnTeamSeasonInjurySourceDto? Source { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("athlete")]
    public EspnLinkDto? Athlete { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto? Team { get; set; }

    public string GetTypeName()
    {
        return Type?.Name ?? Type?.Description ?? "unknown";
    }

    public string GetSourceName()
    {
        return Source?.Description ?? string.Empty;
    }

    public string GetHeadlineText() => Headline ?? ShortComment ?? string.Empty;
    
    public string GetBodyText() => Text ?? LongComment ?? string.Empty;
}
