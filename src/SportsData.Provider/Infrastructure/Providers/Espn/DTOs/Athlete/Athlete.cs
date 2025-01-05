using Newtonsoft.Json;
using SportsData.Core.Converters;

namespace SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Athlete
{
    public class Athlete
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }

        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("uid")]
        public string Uid { get; set; }

        [JsonProperty("guid")]
        public string Guid { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("alternateIds")]
        public AlternateIds AlternateIds { get; set; }

        [JsonProperty("firstName")]
        public string FirstName { get; set; }

        [JsonProperty("lastName")]
        public string LastName { get; set; }

        [JsonProperty("fullName")]
        public string FullName { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("shortName")]
        public string ShortName { get; set; }

        [JsonProperty("weight")]
        public long Weight { get; set; }

        [JsonProperty("displayWeight")]
        public string DisplayWeight { get; set; }

        [JsonProperty("height")]
        public long Height { get; set; }

        [JsonProperty("displayHeight")]
        public string DisplayHeight { get; set; }

        [JsonProperty("age")]
        public long Age { get; set; }

        [JsonProperty("dateOfBirth")]
        public string DateOfBirth { get; set; }

        [JsonProperty("links")]
        public List<Link> Links { get; set; }

        [JsonProperty("birthPlace")]
        public BirthPlace BirthPlace { get; set; }

        [JsonProperty("college")]
        public College College { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("headshot")]
        public Headshot Headshot { get; set; }

        [JsonProperty("jersey")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Jersey { get; set; }

        [JsonProperty("position")]
        public Position Position { get; set; }

        [JsonProperty("injuries")]
        public List<object> Injuries { get; set; }

        [JsonProperty("linked")]
        public bool Linked { get; set; }

        [JsonProperty("team")]
        public College Team { get; set; }

        [JsonProperty("teams")]
        public List<College> Teams { get; set; }

        [JsonProperty("statistics")]
        public College Statistics { get; set; }

        [JsonProperty("notes")]
        public College Notes { get; set; }

        [JsonProperty("experience")]
        public Experience Experience { get; set; }

        [JsonProperty("proAthlete")]
        public College ProAthlete { get; set; }

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("eventLog")]
        public College EventLog { get; set; }

        [JsonProperty("status")]
        public Status Status { get; set; }
    }

    public class AlternateIds
    {
        [JsonProperty("sdr")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Sdr { get; set; }
    }

    public class BirthPlace
    {
        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }
    }

    public class College
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }
    }

    public class Experience
    {
        [JsonProperty("years")]
        public long Years { get; set; }

        [JsonProperty("displayValue")]
        public string DisplayValue { get; set; }

        [JsonProperty("abbreviation")]
        public string Abbreviation { get; set; }
    }

    public class Headshot
    {
        [JsonProperty("href")]
        public Uri Href { get; set; }

        [JsonProperty("alt")]
        public string Alt { get; set; }
    }

    public class Link
    {
        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("rel")]
        public List<string> Rel { get; set; }

        [JsonProperty("href")]
        public Uri Href { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("shortText")]
        public string ShortText { get; set; }

        [JsonProperty("isExternal")]
        public bool IsExternal { get; set; }

        [JsonProperty("isPremium")]
        public bool IsPremium { get; set; }
    }

    public class Position
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }

        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonProperty("leaf")]
        public bool Leaf { get; set; }

        [JsonProperty("parent")]
        public College Parent { get; set; }
    }

    public class Status
    {
        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("abbreviation")]
        public string Abbreviation { get; set; }
    }
}
