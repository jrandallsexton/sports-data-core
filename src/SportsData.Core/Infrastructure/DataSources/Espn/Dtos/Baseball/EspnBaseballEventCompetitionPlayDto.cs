#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Collections.Generic;
using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;

public class EspnBaseballEventCompetitionPlayDto : EspnEventCompetitionPlayDtoBase
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("atBatId")]
    public string? AtBatId { get; set; }

    [JsonPropertyName("atBatPitchNumber")]
    public int? AtBatPitchNumber { get; set; }

    [JsonPropertyName("batOrder")]
    public int? BatOrder { get; set; }

    [JsonPropertyName("bats")]
    public EspnAthleteHandDto? Bats { get; set; }

    [JsonPropertyName("pitches")]
    public EspnAthleteHandDto? Pitches { get; set; }

    [JsonPropertyName("summaryType")]
    public string? SummaryType { get; set; }

    [JsonPropertyName("pitchCount")]
    public EspnBaseballCountDto? PitchCount { get; set; }

    [JsonPropertyName("resultCount")]
    public EspnBaseballCountDto? ResultCount { get; set; }

    [JsonPropertyName("pitchCoordinate")]
    public EspnBaseballCoordinateDto? PitchCoordinate { get; set; }

    [JsonPropertyName("pitchType")]
    public EspnBaseballPitchTypeDto? PitchType { get; set; }

    [JsonPropertyName("pitchVelocity")]
    public int? PitchVelocity { get; set; }

    [JsonPropertyName("strikeType")]
    public string? StrikeType { get; set; }

    [JsonPropertyName("hitCoordinate")]
    public EspnBaseballCoordinateDto? HitCoordinate { get; set; }

    [JsonPropertyName("trajectory")]
    public string? Trajectory { get; set; }

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

    // participants[].type is "pitcher" or "batter"; athlete refs need
    // resolution to canonical Athlete IDs (Phase 2). Captured here so the
    // wire shape is complete; not yet persisted to entity columns.
    [JsonPropertyName("participants")]
    public List<EspnEventCompetitionPlayParticipantDto>? Participants { get; set; }

    // Flavor + analytics fields that ride on play-result rows. Kept
    // optional; not persisted in this PR.
    [JsonPropertyName("previousPlayText")]
    public string? PreviousPlayText { get; set; }

    [JsonPropertyName("shortPreviousPlayText")]
    public string? ShortPreviousPlayText { get; set; }

    [JsonPropertyName("alternativeType")]
    public EspnEventCompetitionPlayTypeDto? AlternativeType { get; set; }

    [JsonPropertyName("probability")]
    public EspnLinkDto? Probability { get; set; }
}

public class EspnBaseballCountDto
{
    [JsonPropertyName("balls")]
    public int Balls { get; set; }

    [JsonPropertyName("strikes")]
    public int Strikes { get; set; }
}
