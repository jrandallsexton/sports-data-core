#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    // http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/powerindex
    public class EspnEventCompetitionPowerIndexDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }

        [JsonPropertyName("season")]
        public int Season { get; set; }

        [JsonPropertyName("stats")]
        public List<EspnEventCompetitionPowerIndexStat> Stats { get; set; }
    }

    public class EspnEventCompetitionPowerIndexStat
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

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