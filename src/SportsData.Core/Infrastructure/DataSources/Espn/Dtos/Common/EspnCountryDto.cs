using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnCountryDto
{
    [JsonPropertyName("alternateId")]
    public string AlternateId { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }
}