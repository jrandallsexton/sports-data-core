#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnEventCompetitionDrivePeriodDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }
}