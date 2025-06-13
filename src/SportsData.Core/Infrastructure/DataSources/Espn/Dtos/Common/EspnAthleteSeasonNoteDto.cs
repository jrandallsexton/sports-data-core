using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618

/// <summary>
/// Represents a note or update related to an athlete's season, as provided by ESPN.
/// </summary>
/// <remarks>This data transfer object (DTO) is used to encapsulate information about a specific note or update,
/// including its identifier, type, date, headline, content, and source. It is typically used to convey athlete-related
/// updates in a structured format.</remarks>
public class EspnAthleteSeasonNoteDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonPropertyName("headline")]
    public string Headline { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; }
}