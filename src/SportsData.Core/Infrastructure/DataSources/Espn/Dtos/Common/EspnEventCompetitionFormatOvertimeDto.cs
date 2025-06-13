using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnEventCompetitionFormatOvertimeDto
{
    [JsonPropertyName("periods")]
    public int Periods { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; }
}