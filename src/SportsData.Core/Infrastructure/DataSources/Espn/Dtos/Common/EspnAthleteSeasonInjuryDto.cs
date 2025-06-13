#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// Represents an athlete's injury information for a specific season, as provided by ESPN.
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/4426333/injuries/171387
    /// </summary>
    /// <remarks>This data transfer object (DTO) contains details about an athlete's injury, including
    /// comments, status,  and associated metadata such as the athlete, team, and source of the information. It is
    /// typically used  to convey injury-related data in applications that consume ESPN's APIs or similar data
    /// sources.</remarks>
    public class EspnAthleteSeasonInjuryDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("longComment")]
        public string LongComment { get; set; }

        [JsonPropertyName("shortComment")]
        public string ShortComment { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("athlete")]
        public EspnLinkDto Athlete { get; set; }

        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }

        [JsonPropertyName("source")]
        public EspnAthleteSeasonInjurySource Source { get; set; }

        [JsonPropertyName("type")]
        public EspnAthleteSeasonInjurySource Type { get; set; }
    }

    public class EspnAthleteSeasonInjurySource
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }
    }

    public class EspnAthleteSeasonInjurySourceType
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }
    }
}