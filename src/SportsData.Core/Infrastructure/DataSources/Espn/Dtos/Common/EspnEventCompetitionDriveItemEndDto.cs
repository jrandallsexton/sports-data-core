#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnEventCompetitionDriveItemEndDto
{
    [JsonPropertyName("period")]
    public EspnEventCompetitionDriveItemPeriodDto Period { get; set; }

    [JsonPropertyName("clock")]
    public EspnClockDto Clock { get; set; }

    [JsonPropertyName("yardLine")]
    public int YardLine { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("down")]
    public int Down { get; set; }

    [JsonPropertyName("distance")]
    public int Distance { get; set; }

    [JsonPropertyName("yardsToEndzone")]
    public int YardsToEndzone { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto Team { get; set; }

    [JsonPropertyName("downDistanceText")]
    public string DownDistanceText { get; set; }

    [JsonPropertyName("shortDownDistanceText")]
    public string ShortDownDistanceText { get; set; }

    [JsonPropertyName("possessionText")]
    public string PossessionText { get; set; }
}