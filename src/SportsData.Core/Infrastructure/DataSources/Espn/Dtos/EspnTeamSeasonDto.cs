using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using SportsData.Core.Converters;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnTeamSeasonDto
    {
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
        public List<EspnTeamSeasonLogo> Logos { get; set; }

        [JsonPropertyName("record")]
        public EspnTeamSeasonResourceIndex Record { get; set; }

        [JsonPropertyName("oddsRecords")]
        public EspnTeamSeasonResourceIndex OddsRecords { get; set; }

        [JsonPropertyName("athletes")]
        public EspnTeamSeasonResourceIndex Athletes { get; set; }

        [JsonPropertyName("venue")]
        public EspnTeamSeasonVenue Venue { get; set; }

        [JsonPropertyName("groups")]
        public EspnTeamSeasonResourceIndex Groups { get; set; }

        [JsonPropertyName("ranks")]
        public EspnTeamSeasonResourceIndex Ranks { get; set; }

        [JsonPropertyName("links")]
        public List<EspnTeamSeasonLink> Links { get; set; }

        [JsonPropertyName("injuries")]
        public EspnTeamSeasonResourceIndex Injuries { get; set; }

        [JsonPropertyName("notes")]
        public EspnTeamSeasonResourceIndex Notes { get; set; }

        [JsonPropertyName("againstTheSpreadRecords")]
        public EspnTeamSeasonResourceIndex AgainstTheSpreadRecords { get; set; }

        [JsonPropertyName("awards")]
        public EspnTeamSeasonResourceIndex Awards { get; set; }

        [JsonPropertyName("franchise")]
        public EspnTeamSeasonResourceIndex Franchise { get; set; }

        [JsonPropertyName("projection")]
        public EspnTeamSeasonResourceIndex Projection { get; set; }

        [JsonPropertyName("events")]
        public EspnTeamSeasonResourceIndex Events { get; set; }

        [JsonPropertyName("recruiting")]
        public EspnTeamSeasonResourceIndex Recruiting { get; set; }

        [JsonPropertyName("college")]
        public EspnTeamSeasonResourceIndex College { get; set; }
    }

    public class EspnTeamSeasonResourceIndex
    {
        // $ref removed
    }

    public class EspnTeamSeasonAlternateIds
    {
        [JsonPropertyName("sdr")]
        [JsonConverter(typeof(ParseStringToLongConverter))]
        public long Sdr { get; set; }
    }

    public class EspnTeamSeasonLink
    {
        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("rel")]
        public List<string> Rel { get; set; }

        [JsonPropertyName("Href")]
        public string Href { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("shortText")]
        public string ShortText { get; set; }

        [JsonPropertyName("isExternal")]
        public bool IsExternal { get; set; }

        [JsonPropertyName("isPremium")]
        public bool IsPremium { get; set; }
    }

    public class EspnTeamSeasonLogo
    {
        [JsonPropertyName("Href")]
        public Uri Href { get; set; }

        [JsonPropertyName("width")]
        public long Width { get; set; }

        [JsonPropertyName("height")]
        public long Height { get; set; }

        [JsonPropertyName("alt")]
        public string Alt { get; set; }

        [JsonPropertyName("rel")]
        public List<string> Rel { get; set; }
    }

    public class EspnTeamSeasonVenue
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(ParseStringToLongConverter))]
        public long Id { get; set; }

        [JsonPropertyName("fullName")]
        public string FullName { get; set; }

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; }

        [JsonPropertyName("address")]
        public EspnTeamSeasonAddress Address { get; set; }

        [JsonPropertyName("capacity")]
        public long Capacity { get; set; }

        [JsonPropertyName("grass")]
        public bool Grass { get; set; }

        [JsonPropertyName("indoor")]
        public bool Indoor { get; set; }

        [JsonPropertyName("images")]
        public List<EspnTeamSeasonLogo> Images { get; set; }
    }

    public class EspnTeamSeasonAddress
    {
        [JsonPropertyName("city")]
        public string City { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("zipCode")]
        [JsonConverter(typeof(ParseStringToLongConverter))]
        public long ZipCode { get; set; }
    }
}
