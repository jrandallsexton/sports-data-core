using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnEventCompetitionPlayStartEndDto
{
    [JsonPropertyName("down")]
    public int Down { get; set; }

    [JsonPropertyName("distance")]
    public int Distance { get; set; }

    [JsonPropertyName("yardLine")]
    public int YardLine { get; set; }

    [JsonPropertyName("yardsToEndzone")]
    public int YardsToEndzone { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto Team { get; set; }
}