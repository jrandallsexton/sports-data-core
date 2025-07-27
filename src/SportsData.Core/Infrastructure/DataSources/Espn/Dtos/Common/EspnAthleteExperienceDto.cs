#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

public class EspnAthleteExperienceDto
{
    [JsonPropertyName("years")]
    public int Years { get; set; }

    [JsonPropertyName("displayValue")]
    public string DisplayValue { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }
}