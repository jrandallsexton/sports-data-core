using Newtonsoft.Json;
using SportsData.Core.Converters;

namespace SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Franchise
{
    public class Franchise
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }

        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("uid")]
        public string Uid { get; set; }

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

        [JsonProperty("isActive")]
        public bool IsActive { get; set; }

        [JsonProperty("logos")]
        public List<Logo> Logos { get; set; }

        [JsonProperty("venue")]
        public Venue Venue { get; set; }

        [JsonProperty("team")]
        public Awards Team { get; set; }

        [JsonProperty("awards")]
        public Awards Awards { get; set; }
    }

    public class Awards
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }
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
        public List<Image> Images { get; set; }
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

    public class Image
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
}
