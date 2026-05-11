using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;

public class EspnBaseballPitchTypeDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("abbreviation")]
    public string? Abbreviation { get; set; }
}
