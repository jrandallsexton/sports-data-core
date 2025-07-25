#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnEventCompetitionDriveItemPlayDto
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("sequenceNumber")]
        public string SequenceNumber { get; set; }

        [JsonPropertyName("type")]
        public EspnEventCompetitionDriveItemPlayTypeDto Type { get; set; }

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
        public EspnEventCompetitionDriveItemPlayPeriodDto Period { get; set; }

        [JsonPropertyName("clock")]
        public EspnClockDto Clock { get; set; }

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

        [JsonPropertyName("participants")]
        public List<EspnEventCompetitionDriveItemPlayParticipantDto> Participants { get; set; }

        [JsonPropertyName("probability")]
        public EspnLinkDto Probability { get; set; }

        [JsonPropertyName("wallclock")]
        public DateTime Wallclock { get; set; }

        [JsonPropertyName("drive")]
        public EspnLinkDto Drive { get; set; }

        [JsonPropertyName("start")]
        public EspnEventCompetitionDriveItemPlayStartEndDto Start { get; set; }

        [JsonPropertyName("end")]
        public EspnEventCompetitionDriveItemPlayStartEndDto End { get; set; }

        [JsonPropertyName("statYardage")]
        public int StatYardage { get; set; }
    }

    public class EspnEventCompetitionDriveItemPlayParticipantDto
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

    public class EspnEventCompetitionDriveItemPlayPeriodDto
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }
    }

    public class EspnEventCompetitionDriveItemPlayStartEndDto
    {
        [JsonPropertyName("down")]
        public int Down { get; set; }

        [JsonPropertyName("distance")]
        public int Distance { get; set; }

        [JsonPropertyName("yardLine")]
        public int YardLine { get; set; }

        [JsonPropertyName("yardsToEndzone")]
        public int YardsToEndzone { get; set; }

        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }
    }

    public class EspnEventCompetitionDriveItemPlayTypeDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }
    }


}
