#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnEventCompetitionPlayDto
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; }

        /// <summary>
        /// ESPN Id (likely delete)
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Sequence number of the play in the game
        /// </summary>
        [JsonPropertyName("sequenceNumber")]
        public string SequenceNumber { get; set; }

        /// <summary>
        /// Type of the play, e.g., "pass", "run", etc.
        /// </summary>
        [JsonPropertyName("type")]
        public EspnEventCompetitionPlayTypeDto Type { get; set; }

        /// <summary>
        /// ex: "Miller Moss pass complete to Lake McRee for 17 yds to the USC 30 for a 1ST down"
        /// </summary>
        [JsonPropertyName("text")]
        public string Text { get; set; }

        /// <summary>
        /// ex: "M. Moss pass to L. McRee for 17 yds for a 1ST down"
        /// </summary>
        [JsonPropertyName("shortText")]
        public string ShortText { get; set; }

        /// <summary>
        /// ex: "Miller Moss pass complete to Lake McRee for 17 yds to the USC 30 for a 1ST down"
        /// </summary>
        [JsonPropertyName("alternativeText")]
        public string AlternativeText { get; set; }

        /// <summary>
        /// ex: "M. Moss pass to L. McRee for 17 yds for a 1ST down"
        /// </summary>
        [JsonPropertyName("shortAlternativeText")]
        public string ShortAlternativeText { get; set; }

        /// <summary>
        /// Current away score
        /// </summary>
        [JsonPropertyName("awayScore")]
        public int AwayScore { get; set; }

        /// <summary>
        /// Current home score
        /// </summary>
        [JsonPropertyName("homeScore")]
        public int HomeScore { get; set; }

        /// <summary>
        /// Current period of the game (DTO is simply an int)
        /// </summary>
        [JsonPropertyName("period")]
        public EspnEventCompetitionPlayPeriodDto Period { get; set; }

        /// <summary>
        /// Clock information for the play
        /// </summary>
        [JsonPropertyName("clock")]
        public EspnClockDto Clock { get; set; }

        /// <summary>
        /// Did this play result in a score?
        /// </summary>
        [JsonPropertyName("scoringPlay")]
        public bool ScoringPlay { get; set; }

        /// <summary>
        /// Note if this play is a priority play, such as a scoring play or a turnover
        /// </summary>
        [JsonPropertyName("priority")]
        public bool Priority { get; set; }

        /// <summary>
        /// If this play resulted in a score, how many points were scored?
        /// </summary>
        [JsonPropertyName("scoreValue")]
        public int ScoreValue { get; set; }

        /// <summary>
        /// Last modified date of the play
        /// </summary>
        [JsonPropertyName("modified")]
        public DateTime Modified { get; set; }

        /// <summary>
        /// FranchiseSeason Id of the team that made the play
        /// </summary>
        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }

        /// <summary>
        /// Athletes involved in the play
        /// </summary>
        [JsonPropertyName("participants")]
        public List<EspnEventCompetitionPlayParticipantDto> Participants { get; set; }

        /// <summary>
        /// Link to the play's probability data, if available
        /// </summary>
        [JsonPropertyName("probability")]
        public EspnLinkDto Probability { get; set; }

        /// <summary>
        /// Actual play start and end information
        /// </summary>
        [JsonPropertyName("wallclock")]
        public DateTime Wallclock { get; set; }

        /// <summary>
        /// Link to the drive this play is part of
        /// </summary>
        [JsonPropertyName("drive")]
        public EspnLinkDto Drive { get; set; }

        /// <summary>
        /// Data about the start of the play, such as down, distance, yard line, and yards to end zone
        /// </summary>
        [JsonPropertyName("start")]
        public EspnEventCompetitionPlayStartEndDto Start { get; set; }

        /// <summary>
        /// Data about the end of the play, such as down, distance, yard line, and yards to end zone
        /// </summary>
        [JsonPropertyName("end")]
        public EspnEventCompetitionPlayStartEndDto End { get; set; }

        /// <summary>
        /// Yardage gained or lost on the play, used for statistics
        /// </summary>
        [JsonPropertyName("statYardage")]
        public int StatYardage { get; set; }

        [JsonPropertyName("scoringType")]
        public EspnEventCompetitionPlayScoringTypeDto ScoringType { get; set; }

        [JsonPropertyName("pointAfterAttempt")]
        public EspnEventCompetitionPlayPointAfterAttemptDto PointAfterAttempt { get; set; }
    }
}
