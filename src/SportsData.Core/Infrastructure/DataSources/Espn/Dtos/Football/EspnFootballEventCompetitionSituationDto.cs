#pragma warning disable CS8618

using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

public class EspnFootballEventCompetitionSituationDto : EspnEventCompetitionSituationDtoBase
{
    [JsonPropertyName("down")]
    public int Down { get; set; }

    [JsonPropertyName("yardLine")]
    public int YardLine { get; set; }

    [JsonPropertyName("distance")]
    public int Distance { get; set; }

    [JsonPropertyName("isRedZone")]
    public bool IsRedZone { get; set; }

    [JsonPropertyName("homeTimeouts")]
    public int HomeTimeouts { get; set; }

    [JsonPropertyName("awayTimeouts")]
    public int AwayTimeouts { get; set; }
}
