using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnEventCompetitionNoteDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("headline")]
    public string Headline { get; set; }
}