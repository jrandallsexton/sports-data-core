#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

public class EspnFootballEventCompetitionDto : EspnEventCompetitionDtoBase
{
    [JsonPropertyName("dateValid")]
    public bool DateValid { get; set; }

    [JsonPropertyName("drives")]
    public EspnLinkDto Drives { get; set; }

    [JsonPropertyName("hasDefensiveStats")]
    public bool HasDefensiveStats { get; set; }
}
