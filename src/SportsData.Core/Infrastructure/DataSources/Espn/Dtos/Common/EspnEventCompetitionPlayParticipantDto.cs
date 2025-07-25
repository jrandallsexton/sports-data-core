using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnEventCompetitionPlayParticipantDto
{
    [JsonPropertyName("athlete")]
    public EspnLinkDto Athlete { get; set; }

    [JsonPropertyName("position")]
    public EspnLinkDto Position { get; set; }

    [JsonPropertyName("statistics")]
    public EspnLinkDto Statistics { get; set; }

    [JsonPropertyName("stats")]
    public List<EspnEventCompetitionParticipantStatDto> Stats { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }
}