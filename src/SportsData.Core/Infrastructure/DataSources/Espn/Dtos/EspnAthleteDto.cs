using Newtonsoft.Json;

using SportsData.Core.Converters;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnAthleteDto
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
        public EspnAthleteAlternateIds AlternateIds { get; set; }

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
        public decimal Weight { get; set; }

        [JsonProperty("displayWeight")]
        public string DisplayWeight { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("displayHeight")]
        public string DisplayHeight { get; set; }

        [JsonProperty("age")]
        public int Age { get; set; }

        [JsonProperty("dateOfBirth")]
        public string DateOfBirth { get; set; }

        [JsonProperty("links")]
        public List<EspnAthleteLink> Links { get; set; }

        [JsonProperty("birthPlace")]
        public EspnAthleteBirthPlace BirthPlace { get; set; }

        [JsonProperty("college")]
        public EspnAthleteCollege College { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("headshot")]
        public EspnAthleteHeadshot? Headshot { get; set; }

        [JsonProperty("jersey")]
        [JsonConverter(typeof(ParseStringConverter))]
        public int Jersey { get; set; }

        [JsonProperty("position")]
        public EspnAthletePosition Position { get; set; }

        [JsonProperty("injuries")]
        public List<object> Injuries { get; set; }

        [JsonProperty("linked")]
        public bool Linked { get; set; }

        [JsonProperty("team")]
        public EspnAthleteCollege Team { get; set; }

        [JsonProperty("teams")]
        public List<EspnAthleteCollege> Teams { get; set; }

        [JsonProperty("statistics")]
        public EspnAthleteCollege Statistics { get; set; }

        [JsonProperty("notes")]
        public EspnAthleteCollege Notes { get; set; }

        [JsonProperty("experience")]
        public EspnAthleteExperience Experience { get; set; }

        [JsonProperty("proAthlete")]
        public EspnAthleteCollege ProAthlete { get; set; }

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("eventLog")]
        public EspnAthleteCollege EventLog { get; set; }

        [JsonProperty("status")]
        public EspnAthleteStatus Status { get; set; }
    }

    public class EspnAthleteAlternateIds
    {
        [JsonProperty("sdr")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Sdr { get; set; }
    }

    public class EspnAthleteBirthPlace
    {
        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }
    }

    public class EspnAthleteCollege
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }
    }

    public class EspnAthleteExperience
    {
        [JsonProperty("years")]
        public int Years { get; set; }

        [JsonProperty("displayValue")]
        public string DisplayValue { get; set; }

        [JsonProperty("abbreviation")]
        public string Abbreviation { get; set; }
    }

    public class EspnAthleteHeadshot
    {
        [JsonProperty("href")]
        public Uri Href { get; set; }

        [JsonProperty("alt")]
        public string Alt { get; set; }
    }

    public class EspnAthleteLink
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

    public class EspnAthletePosition
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
        public EspnAthleteCollege Parent { get; set; }
    }

    public class EspnAthleteStatus
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
