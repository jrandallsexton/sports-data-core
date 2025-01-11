using Newtonsoft.Json;

using SportsData.Core.Converters;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnTeamSeasonDto
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }

        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("uid")]
        public string Uid { get; set; }

        [JsonProperty("alternateIds")]
        public EspnTeamSeasonAlternateIds AlternateIds { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("nickname")]
        public string Nickname { get; set; }

        [JsonProperty("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("shortDisplayName")]
        public string ShortDisplayName { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("alternateColor")]
        public string AlternateColor { get; set; }

        [JsonProperty("isActive")]
        public bool IsActive { get; set; }

        [JsonProperty("isAllStar")]
        public bool IsAllStar { get; set; }

        [JsonProperty("logos")]
        public List<EspnTeamSeasonLogo> Logos { get; set; }

        [JsonProperty("record")]
        public EspnTeamSeasonResourceIndex Record { get; set; }

        [JsonProperty("oddsRecords")]
        public EspnTeamSeasonResourceIndex OddsRecords { get; set; }

        [JsonProperty("athletes")]
        public EspnTeamSeasonResourceIndex Athletes { get; set; }

        [JsonProperty("venue")]
        public EspnTeamSeasonVenue Venue { get; set; }

        [JsonProperty("groups")]
        public EspnTeamSeasonResourceIndex Groups { get; set; }

        [JsonProperty("ranks")]
        public EspnTeamSeasonResourceIndex Ranks { get; set; }

        [JsonProperty("links")]
        public List<EspnTeamSeasonLink> Links { get; set; }

        [JsonProperty("injuries")]
        public EspnTeamSeasonResourceIndex Injuries { get; set; }

        [JsonProperty("notes")]
        public EspnTeamSeasonResourceIndex Notes { get; set; }

        [JsonProperty("againstTheSpreadRecords")]
        public EspnTeamSeasonResourceIndex AgainstTheSpreadRecords { get; set; }

        [JsonProperty("awards")]
        public EspnTeamSeasonResourceIndex Awards { get; set; }

        [JsonProperty("franchise")]
        public EspnTeamSeasonResourceIndex Franchise { get; set; }

        [JsonProperty("projection")]
        public EspnTeamSeasonResourceIndex Projection { get; set; }

        [JsonProperty("events")]
        public EspnTeamSeasonResourceIndex Events { get; set; }

        [JsonProperty("recruiting")]
        public EspnTeamSeasonResourceIndex Recruiting { get; set; }

        [JsonProperty("college")]
        public EspnTeamSeasonResourceIndex College { get; set; }
    }

    public class EspnTeamSeasonResourceIndex
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }
    }

    public class EspnTeamSeasonAlternateIds
    {
        [JsonProperty("sdr")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Sdr { get; set; }
    }

    public class EspnTeamSeasonLink
    {
        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("rel")]
        public List<string> Rel { get; set; }

        [JsonProperty("href")]
        public string Href { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("shortText")]
        public string ShortText { get; set; }

        [JsonProperty("isExternal")]
        public bool IsExternal { get; set; }

        [JsonProperty("isPremium")]
        public bool IsPremium { get; set; }
    }

    public class EspnTeamSeasonLogo
    {
        [JsonProperty("href")]
        public Uri Href { get; set; }

        [JsonProperty("width")]
        public long Width { get; set; }

        [JsonProperty("height")]
        public long Height { get; set; }

        [JsonProperty("alt")]
        public string Alt { get; set; }

        [JsonProperty("rel")]
        public List<string> Rel { get; set; }
    }

    public class EspnTeamSeasonVenue
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }

        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("fullName")]
        public string FullName { get; set; }

        [JsonProperty("shortName")]
        public string ShortName { get; set; }

        [JsonProperty("address")]
        public EspnTeamSeasonAddress Address { get; set; }

        [JsonProperty("capacity")]
        public long Capacity { get; set; }

        [JsonProperty("grass")]
        public bool Grass { get; set; }

        [JsonProperty("indoor")]
        public bool Indoor { get; set; }

        [JsonProperty("images")]
        public List<EspnTeamSeasonLogo> Images { get; set; }
    }

    public class EspnTeamSeasonAddress
    {
        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("zipCode")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long ZipCode { get; set; }
    }
}
