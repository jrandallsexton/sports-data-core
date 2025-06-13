#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnEventCompetitionDriveItemScoringTypeDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }
}