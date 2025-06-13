using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnEventCompetitionFormatDto
{
    [JsonPropertyName("regulation")]
    public EspnEventCompetitionFormatRegulationDto Regulation { get; set; }

    [JsonPropertyName("overtime")]
    public EspnEventCompetitionFormatOvertimeDto Overtime { get; set; }
}