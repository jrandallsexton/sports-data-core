#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;

public class EspnBaseballEventCompetitionPlayDto : EspnEventCompetitionPlayDtoBase
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("atBatId")]
    public string? AtBatId { get; set; }

    [JsonPropertyName("summaryType")]
    public string? SummaryType { get; set; }

    [JsonPropertyName("pitchCount")]
    public EspnBaseballCountDto? PitchCount { get; set; }

    [JsonPropertyName("resultCount")]
    public EspnBaseballCountDto? ResultCount { get; set; }

    [JsonPropertyName("outs")]
    public int Outs { get; set; }

    [JsonPropertyName("rbiCount")]
    public int RbiCount { get; set; }

    [JsonPropertyName("awayHits")]
    public int AwayHits { get; set; }

    [JsonPropertyName("homeHits")]
    public int HomeHits { get; set; }

    [JsonPropertyName("awayErrors")]
    public int AwayErrors { get; set; }

    [JsonPropertyName("homeErrors")]
    public int HomeErrors { get; set; }

    [JsonPropertyName("doublePlay")]
    public bool DoublePlay { get; set; }

    [JsonPropertyName("triplePlay")]
    public bool TriplePlay { get; set; }
}

public class EspnBaseballCountDto
{
    [JsonPropertyName("balls")]
    public int Balls { get; set; }

    [JsonPropertyName("strikes")]
    public int Strikes { get; set; }
}
