#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnEventCompetitionDriveItemTypeDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }
}