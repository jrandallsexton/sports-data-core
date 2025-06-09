using SportsData.Core.Converters;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnTeamSeasonDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("id")]
        [JsonConverter(typeof(ParseStringToLongConverter))]
        public long Id { get; set; }

        [JsonPropertyName("uid")]
        public string Uid { get; set; }

        [JsonPropertyName("alternateIds")]
        public EspnTeamSeasonAlternateIds AlternateIds { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("shortDisplayName")]
        public string ShortDisplayName { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; }

        [JsonPropertyName("alternateColor")]
        public string AlternateColor { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("isAllStar")]
        public bool IsAllStar { get; set; }

        [JsonPropertyName("logos")]
        public List<EspnImageDto> Logos { get; set; }

        [JsonPropertyName("record")]
        public EspnResourceIndexItem Record { get; set; }

        [JsonPropertyName("oddsRecords")]
        public EspnResourceIndexItem OddsRecords { get; set; }

        [JsonPropertyName("athletes")]
        public EspnResourceIndexItem Athletes { get; set; }

        [JsonPropertyName("venue")]
        public EspnVenueDto Venue { get; set; }

        [JsonPropertyName("groups")]
        public EspnResourceIndexItem Groups { get; set; }

        [JsonPropertyName("ranks")]
        public EspnResourceIndexItem Ranks { get; set; }

        [JsonPropertyName("links")]
        public List<EspnLinkFullDto> Links { get; set; }

        [JsonPropertyName("injuries")]
        public EspnResourceIndexItem Injuries { get; set; }

        [JsonPropertyName("notes")]
        public EspnResourceIndexItem Notes { get; set; }

        [JsonPropertyName("againstTheSpreadRecords")]
        public EspnResourceIndexItem AgainstTheSpreadRecords { get; set; }

        [JsonPropertyName("awards")]
        public EspnResourceIndexItem Awards { get; set; }

        [JsonPropertyName("franchise")]
        public EspnResourceIndexItem Franchise { get; set; }

        [JsonPropertyName("projection")]
        public EspnResourceIndexItem Projection { get; set; }

        [JsonPropertyName("events")]
        public EspnResourceIndexItem Events { get; set; }

        [JsonPropertyName("recruiting")]
        public EspnResourceIndexItem Recruiting { get; set; }

        [JsonPropertyName("college")]
        public EspnResourceIndexItem College { get; set; }
    }

    public class EspnTeamSeasonAlternateIds
    {
        [JsonPropertyName("sdr")]
        [JsonConverter(typeof(ParseStringToLongConverter))]
        public long Sdr { get; set; }
    }
}
