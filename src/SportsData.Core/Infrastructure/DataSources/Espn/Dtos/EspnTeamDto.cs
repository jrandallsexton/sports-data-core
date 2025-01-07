using Newtonsoft.Json;

using SportsData.Core.Converters;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnTeamDto
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }

        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("uid")]
        public string Uid { get; set; }

        [JsonProperty("alternateIds")]
        public EspnTeamAlternateIds AlternateIds { get; set; }

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
        public List<EspnTeamLogo> Logos { get; set; }

        [JsonProperty("record")]
        public EspnTeamAgainstTheSpreadRecords Record { get; set; }

        [JsonProperty("oddsRecords")]
        public EspnTeamAgainstTheSpreadRecords OddsRecords { get; set; }

        [JsonProperty("athletes")]
        public EspnTeamAgainstTheSpreadRecords Athletes { get; set; }

        [JsonProperty("venue")]
        public EspnTeamVenue Venue { get; set; }

        [JsonProperty("groups")]
        public EspnTeamAgainstTheSpreadRecords Groups { get; set; }

        [JsonProperty("ranks")]
        public EspnTeamAgainstTheSpreadRecords Ranks { get; set; }

        [JsonProperty("links")]
        public List<EspnTeamLink> Links { get; set; }

        [JsonProperty("injuries")]
        public EspnTeamAgainstTheSpreadRecords Injuries { get; set; }

        [JsonProperty("notes")]
        public EspnTeamAgainstTheSpreadRecords Notes { get; set; }

        [JsonProperty("againstTheSpreadRecords")]
        public EspnTeamAgainstTheSpreadRecords AgainstTheSpreadRecords { get; set; }

        [JsonProperty("awards")]
        public EspnTeamAgainstTheSpreadRecords Awards { get; set; }

        [JsonProperty("franchise")]
        public EspnTeamAgainstTheSpreadRecords Franchise { get; set; }

        [JsonProperty("projection")]
        public EspnTeamAgainstTheSpreadRecords Projection { get; set; }

        [JsonProperty("events")]
        public EspnTeamAgainstTheSpreadRecords Events { get; set; }

        [JsonProperty("recruiting")]
        public EspnTeamAgainstTheSpreadRecords Recruiting { get; set; }

        [JsonProperty("college")]
        public EspnTeamAgainstTheSpreadRecords College { get; set; }
    }

    public class EspnTeamAgainstTheSpreadRecords
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }
    }

    public class EspnTeamAlternateIds
    {
        [JsonProperty("sdr")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Sdr { get; set; }
    }

    public class EspnTeamLink
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

    public class EspnTeamLogo
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

    public class EspnTeamVenue
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
        public EspnTeamAddress Address { get; set; }

        [JsonProperty("capacity")]
        public long Capacity { get; set; }

        [JsonProperty("grass")]
        public bool Grass { get; set; }

        [JsonProperty("indoor")]
        public bool Indoor { get; set; }

        [JsonProperty("images")]
        public List<EspnTeamLogo> Images { get; set; }
    }

    public class EspnTeamAddress
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
