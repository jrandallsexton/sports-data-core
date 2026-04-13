#pragma warning disable CS8618

using System.Collections.Generic;
using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;

public class EspnBaseballEventCompetitionSituationDto : EspnEventCompetitionSituationDtoBase
{
    [JsonPropertyName("balls")]
    public int Balls { get; set; }

    [JsonPropertyName("strikes")]
    public int Strikes { get; set; }

    [JsonPropertyName("outs")]
    public int Outs { get; set; }

    [JsonPropertyName("onFirst")]
    public EspnBaseballSituationPlayerDto? OnFirst { get; set; }

    [JsonPropertyName("onSecond")]
    public EspnBaseballSituationPlayerDto? OnSecond { get; set; }

    [JsonPropertyName("onThird")]
    public EspnBaseballSituationPlayerDto? OnThird { get; set; }

    [JsonPropertyName("pitcher")]
    public EspnBaseballSituationPlayerDto? Pitcher { get; set; }

    [JsonPropertyName("batter")]
    public EspnBaseballSituationPlayerDto? Batter { get; set; }

    [JsonPropertyName("situationNotes")]
    public List<EspnBaseballSituationNoteDto>? SituationNotes { get; set; }
}

public class EspnBaseballSituationPlayerDto
{
    [JsonPropertyName("playerId")]
    public int PlayerId { get; set; }

    [JsonPropertyName("period")]
    public int Period { get; set; }

    [JsonPropertyName("athlete")]
    public EspnLinkDto? Athlete { get; set; }

    [JsonPropertyName("position")]
    public EspnLinkDto? Position { get; set; }

    [JsonPropertyName("statistics")]
    public EspnLinkDto? Statistics { get; set; }
}

public class EspnBaseballSituationNoteDto
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
