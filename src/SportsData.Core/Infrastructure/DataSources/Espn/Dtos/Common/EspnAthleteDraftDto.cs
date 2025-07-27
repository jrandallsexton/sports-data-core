#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

public class EspnAthleteDraftDto
{
    [JsonPropertyName("displayText")]
    public string Display { get; set; }

    [JsonPropertyName("round")]
    public int Round { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("selection")]
    public int Selection { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto Team { get; set; }

    [JsonPropertyName("pick")]
    public EspnLinkDto Pick { get; set; }
}