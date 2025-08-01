#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/predictor?lang=en
    /// </summary>
    public class EspnEventCompetitionPredictorDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; }

        [JsonPropertyName("lastModified")]
        public string LastModified { get; set; }

        [JsonPropertyName("homeTeam")]
        public EspnEventCompetitionPredictorTeam HomeTeam { get; set; }

        [JsonPropertyName("awayTeam")]
        public EspnEventCompetitionPredictorTeam AwayTeam { get; set; }
    }

    public class EspnEventCompetitionPredictorTeam
    {
        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }

        [JsonPropertyName("statistics")]
        public List<EspnEventCompetitionPredictorTeamStatistic> Statistics { get; set; }
    }

    public class EspnEventCompetitionPredictorTeamStatistic
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("shortDisplayName")]
        public string ShortDisplayName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; }
    }
}
