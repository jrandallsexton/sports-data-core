#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnEventCompetitionDriveItemParticipantDto
{
    [JsonPropertyName("athlete")]
    public EspnLinkDto Athlete { get; set; }

    [JsonPropertyName("position")]
    public EspnLinkDto Position { get; set; }

    [JsonPropertyName("statistics")]
    public EspnLinkDto Statistics { get; set; }

    [JsonPropertyName("stats")]
    public List<EspnEventCompetitionDriveItemParticipantStatDto> Stats { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }
}