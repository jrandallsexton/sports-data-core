#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// League-wide season stat leaders for one season type — the document
    /// behind ESPN's Season Leaders UI.
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/3/leaders
    /// types/2 = regular season only; types/3 = cumulative through the
    /// postseason. Verified DISTINCT datasets (different totals AND different
    /// leaders in several categories). Default 25 leaders per category;
    /// ?limit= raises it.
    /// </summary>
    public class EspnSeasonTypeLeadersDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("categories")]
        public List<EspnSeasonTypeLeaderCategoryDto> Categories { get; set; }
    }

    public class EspnSeasonTypeLeaderCategoryDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("shortDisplayName")]
        public string ShortDisplayName { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("leaders")]
        public List<EspnSeasonTypeLeaderDto> Leaders { get; set; }
    }

    public class EspnSeasonTypeLeaderDto
    {
        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; }

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        /// <summary>Season-scoped athlete ref (…/seasons/{year}/athletes/{id}).</summary>
        [JsonPropertyName("athlete")]
        public EspnLinkDto Athlete { get; set; }

        /// <summary>Season-scoped team ref (…/seasons/{year}/teams/{id}).</summary>
        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }

        /// <summary>Season-type-scoped statistics ref for the athlete.</summary>
        [JsonPropertyName("statistics")]
        public EspnLinkDto Statistics { get; set; }
    }
}
