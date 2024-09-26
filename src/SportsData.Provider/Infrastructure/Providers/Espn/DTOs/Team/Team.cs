using Newtonsoft.Json;
using SportsData.Core.Converters;

namespace SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Team
{
    public class Team
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }

        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("uid")]
        public string Uid { get; set; }

        [JsonProperty("alternateIds")]
        public AlternateIds AlternateIds { get; set; }

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
        public List<Logo> Logos { get; set; }

        [JsonProperty("record")]
        public AgainstTheSpreadRecords Record { get; set; }

        [JsonProperty("oddsRecords")]
        public AgainstTheSpreadRecords OddsRecords { get; set; }

        [JsonProperty("athletes")]
        public AgainstTheSpreadRecords Athletes { get; set; }

        [JsonProperty("venue")]
        public Venue Venue { get; set; }

        [JsonProperty("groups")]
        public AgainstTheSpreadRecords Groups { get; set; }

        [JsonProperty("ranks")]
        public AgainstTheSpreadRecords Ranks { get; set; }

        [JsonProperty("links")]
        public List<Link> Links { get; set; }

        [JsonProperty("injuries")]
        public AgainstTheSpreadRecords Injuries { get; set; }

        [JsonProperty("notes")]
        public AgainstTheSpreadRecords Notes { get; set; }

        [JsonProperty("againstTheSpreadRecords")]
        public AgainstTheSpreadRecords AgainstTheSpreadRecords { get; set; }

        [JsonProperty("awards")]
        public AgainstTheSpreadRecords Awards { get; set; }

        [JsonProperty("franchise")]
        public AgainstTheSpreadRecords Franchise { get; set; }

        [JsonProperty("projection")]
        public AgainstTheSpreadRecords Projection { get; set; }

        [JsonProperty("events")]
        public AgainstTheSpreadRecords Events { get; set; }

        [JsonProperty("recruiting")]
        public AgainstTheSpreadRecords Recruiting { get; set; }

        [JsonProperty("college")]
        public AgainstTheSpreadRecords College { get; set; }
    }

    public class AgainstTheSpreadRecords
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }
    }

    public class AlternateIds
    {
        [JsonProperty("sdr")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Sdr { get; set; }
    }

    public class Link
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

    public class Logo
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

    public class Venue
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
        public Address Address { get; set; }

        [JsonProperty("capacity")]
        public long Capacity { get; set; }

        [JsonProperty("grass")]
        public bool Grass { get; set; }

        [JsonProperty("indoor")]
        public bool Indoor { get; set; }

        [JsonProperty("images")]
        public List<Logo> Images { get; set; }
    }

    public class Address
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
