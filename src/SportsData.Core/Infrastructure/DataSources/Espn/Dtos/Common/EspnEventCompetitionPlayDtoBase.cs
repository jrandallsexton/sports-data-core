#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

public class EspnEventCompetitionPlayDtoBase : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("sequenceNumber")]
    public string SequenceNumber { get; set; }

    [JsonPropertyName("type")]
    public EspnEventCompetitionPlayTypeDto Type { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("shortText")]
    public string ShortText { get; set; }

    [JsonPropertyName("alternativeText")]
    public string AlternativeText { get; set; }

    [JsonPropertyName("shortAlternativeText")]
    public string ShortAlternativeText { get; set; }

    [JsonPropertyName("awayScore")]
    public int AwayScore { get; set; }

    [JsonPropertyName("homeScore")]
    public int HomeScore { get; set; }

    [JsonPropertyName("period")]
    public EspnEventCompetitionPlayPeriodDto Period { get; set; }

    [JsonPropertyName("scoringPlay")]
    public bool ScoringPlay { get; set; }

    [JsonPropertyName("priority")]
    public bool Priority { get; set; }

    [JsonPropertyName("scoreValue")]
    public int ScoreValue { get; set; }

    [JsonPropertyName("modified")]
    public DateTime Modified { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto Team { get; set; }

    [JsonPropertyName("wallclock")]
    public DateTime Wallclock { get; set; }
}
