#pragma warning disable CS8618

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnEventCompetitionCompetitorStatisticsDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("competition")]
        public EspnLinkDto Competition { get; set; }

        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }

        [JsonPropertyName("splits")]
        public EspnEventCompetitionCompetitorStatisticsSplits Splits { get; set; }
    }

    public class EspnEventCompetitionCompetitorStatisticsSplitCategoryAthlete
    {
        [JsonPropertyName("athlete")]
        public EspnLinkDto Athlete { get; set; }

        [JsonPropertyName("statistics")]
        public EspnLinkDto Statistics { get; set; }
    }

    public class EspnEventCompetitionCompetitorStatisticsSplits
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("categories")]
        public List<EspnEventCompetitionCompetitorStatisticsSplitCategory> Categories { get; set; }
    }

    public class EspnEventCompetitionCompetitorStatisticsSplitCategory
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("shortDisplayName")]
        public string ShortDisplayName { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("stats")]
        public List<Stat> Stats { get; set; }

        [JsonPropertyName("athletes")]
        public List<EspnEventCompetitionCompetitorStatisticsSplitCategoryAthlete> Athletes { get; set; }
    }

    public class Stat
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