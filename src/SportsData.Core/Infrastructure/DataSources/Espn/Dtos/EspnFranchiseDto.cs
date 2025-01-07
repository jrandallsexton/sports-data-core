using Microsoft.Azure.Amqp.Framing;

using Newtonsoft.Json;

using SportsData.Core.Converters;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnFranchiseDto
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
        public List<EspnFranchiseLogo> Logos { get; set; }

        [JsonProperty("venue")]
        public EspnFranchiseVenue Venue { get; set; }

        [JsonProperty("team")]
        public EspnFranchiseAwards Team { get; set; }

        [JsonProperty("awards")]
        public EspnFranchiseAwards Awards { get; set; }
    }

    public class EspnFranchiseAwards
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }
    }

    public class EspnFranchiseLogo
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

    public class EspnFranchiseVenue
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
        public List<EspnFranchiseImage> Images { get; set; }
    }

    public class EspnFranchiseAddress
    {
        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("zipCode")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long ZipCode { get; set; }
    }

    public class EspnFranchiseImage
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
