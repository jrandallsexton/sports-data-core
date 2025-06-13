#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnEventCompetitionDriveItemDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("sequenceNumber")]
    public string SequenceNumber { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto Team { get; set; }

    [JsonPropertyName("endTeam")]
    public EspnLinkDto EndTeam { get; set; }

    [JsonPropertyName("start")]
    public EspnEventCompetitionDriveItemStartDto Start { get; set; }

    [JsonPropertyName("end")]
    public EspnEventCompetitionDriveItemEndDto End { get; set; }

    [JsonPropertyName("timeElapsed")]
    public EspnEventCompetitionDriveItemTimeElapsedDto TimeElapsed { get; set; }

    [JsonPropertyName("yards")]
    public int Yards { get; set; }

    [JsonPropertyName("isScore")]
    public bool IsScore { get; set; }

    [JsonPropertyName("offensivePlays")]
    public int OffensivePlays { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; }

    [JsonPropertyName("shortDisplayResult")]
    public string ShortDisplayResult { get; set; }

    [JsonPropertyName("displayResult")]
    public string DisplayResult { get; set; }

    [JsonPropertyName("source")]
    public EspnEventCompetitionDriveItemSourceDto Source { get; set; }

    [JsonPropertyName("plays")]
    public EspnEventCompetitionDriveItemPlaysDto Plays { get; set; }

    [JsonPropertyName("type")]
    public EspnEventCompetitionDriveItemTypeDto Type { get; set; }

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
    public EspnEventCompetitionDriveItemPeriodDto Period { get; set; }

    [JsonPropertyName("clock")]
    public EspnEventCompetitionDriveItemClockDto Clock { get; set; }

    [JsonPropertyName("scoringPlay")]
    public bool ScoringPlay { get; set; }

    [JsonPropertyName("priority")]
    public bool Priority { get; set; }

    [JsonPropertyName("scoreValue")]
    public int ScoreValue { get; set; }

    [JsonPropertyName("modified")]
    public string Modified { get; set; }

    [JsonPropertyName("participants")]
    public List<EspnEventCompetitionDriveItemParticipantDto> Participants { get; set; }

    [JsonPropertyName("probability")]
    public EspnLinkDto Probability { get; set; }

    [JsonPropertyName("wallclock")]
    public DateTime Wallclock { get; set; }

    [JsonPropertyName("drive")]
    public EspnLinkDto Drive { get; set; }

    [JsonPropertyName("statYardage")]
    public int StatYardage { get; set; }

    [JsonPropertyName("scoringType")]
    public EspnEventCompetitionDriveItemScoringTypeDto ScoringType { get; set; }

    [JsonPropertyName("pointAfterAttempt")]
    public EspnEventCompetitionDriveItemPointAfterAttemptDto PointAfterAttempt { get; set; }
}