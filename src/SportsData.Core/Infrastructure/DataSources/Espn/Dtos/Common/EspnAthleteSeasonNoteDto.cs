using System;
using System.Text.Json.Serialization;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618

/// <summary>
/// Represents an ESPN athlete season note accessed via AthleteSeason.Notes link.
/// </summary>
public class EspnAthleteSeasonNoteDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; } = default!;

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("headline")]
    public string? Headline { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("athlete")]
    public EspnLinkDto? Athlete { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto? Team { get; set; }

    public string GetTypeName()
    {
        return Type ?? "unknown";
    }

    public string GetHeadlineText() => Headline ?? string.Empty;
    
    public string GetBodyText() => Text ?? string.Empty;
}
