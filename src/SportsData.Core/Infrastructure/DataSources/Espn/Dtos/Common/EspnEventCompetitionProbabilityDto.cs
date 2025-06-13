#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// Represents the probability data for a specific competition in an ESPN event,  including win percentages, tie
    /// probability, and related metadata.
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/probabilities
    /// </summary>
    /// <remarks>This DTO is used to encapsulate probability-related information for a competition,  such as
    /// the likelihood of each team winning or the probability of a tie. It also  includes links to related entities
    /// (e.g., competition, play, teams) and metadata  like the last modification timestamp and sequence
    /// number.</remarks>
    public class EspnEventCompetitionProbabilityDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("competition")]
        public EspnLinkDto Competition { get; set; }

        [JsonPropertyName("play")]
        public EspnLinkDto Play { get; set; }

        [JsonPropertyName("homeTeam")]
        public EspnLinkDto HomeTeam { get; set; }

        [JsonPropertyName("awayTeam")]
        public EspnLinkDto AwayTeam { get; set; }

        [JsonPropertyName("tiePercentage")]
        public double TiePercentage { get; set; }

        [JsonPropertyName("homeWinPercentage")]
        public double HomeWinPercentage { get; set; }

        [JsonPropertyName("awayWinPercentage")]
        public double AwayWinPercentage { get; set; }

        [JsonPropertyName("lastModified")]
        public string LastModified { get; set; }

        [JsonPropertyName("sequenceNumber")]
        public string SequenceNumber { get; set; }

        [JsonPropertyName("source")]
        public EspnEventCompetitionGameSourceDto Source { get; set; }

        [JsonPropertyName("secondsLeft")]
        public int SecondsLeft { get; set; }
    }
}
