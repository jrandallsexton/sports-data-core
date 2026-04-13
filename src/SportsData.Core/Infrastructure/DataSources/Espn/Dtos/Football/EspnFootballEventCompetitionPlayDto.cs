#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

public class EspnFootballEventCompetitionPlayDto : EspnEventCompetitionPlayDtoBase
{
    [JsonPropertyName("clock")]
    public EspnClockDto Clock { get; set; }

    [JsonPropertyName("endTeam")]
    public EspnLinkDto EndTeam { get; set; }

    [JsonPropertyName("participants")]
    public List<EspnEventCompetitionPlayParticipantDto> Participants { get; set; }

    [JsonPropertyName("probability")]
    public EspnLinkDto Probability { get; set; }

    [JsonPropertyName("drive")]
    public EspnLinkDto Drive { get; set; }

    [JsonPropertyName("start")]
    public EspnEventCompetitionPlayStartEndDto Start { get; set; }

    [JsonPropertyName("end")]
    public EspnEventCompetitionPlayStartEndDto End { get; set; }

    [JsonPropertyName("statYardage")]
    public int StatYardage { get; set; }

    [JsonPropertyName("teamParticipants")]
    public List<TeamParticipant> TeamParticipants { get; set; }

    [JsonPropertyName("scoringType")]
    public EspnEventCompetitionPlayScoringTypeDto ScoringType { get; set; }

    [JsonPropertyName("pointAfterAttempt")]
    public EspnEventCompetitionPlayPointAfterAttemptDto PointAfterAttempt { get; set; }
}

public class TeamParticipant
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto Team { get; set; }
}
