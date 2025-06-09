using SportsData.Core.Converters;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
#pragma warning disable CS8618 // Non-nullable property is uninitialized

    public class EspnAthleteDto
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(ParseStringToLongConverter))]
        public long Id { get; set; }

        [JsonPropertyName("uid")]
        public string Uid { get; set; }

        [JsonPropertyName("guid")]
        public string? Guid { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("alternateIds")]
        public EspnAlternateIdDto AlternateIds { get; set; }

        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("shortName")]
        public string? ShortName { get; set; }

        [JsonPropertyName("weight")]
        public decimal Weight { get; set; }

        [JsonPropertyName("displayWeight")]
        public string? DisplayWeight { get; set; }

        [JsonPropertyName("height")]
        public decimal Height { get; set; }

        [JsonPropertyName("displayHeight")]
        public string? DisplayHeight { get; set; }

        [JsonPropertyName("age")]
        public int Age { get; set; }

        [JsonPropertyName("dateOfBirth")]
        public string? DateOfBirth { get; set; }

        [JsonPropertyName("links")]
        public List<EspnLinkFullDto>? Links { get; set; }

        [JsonPropertyName("birthPlace")]
        public EspnAthleteBirthPlaceDto? BirthPlace { get; set; }

        [JsonPropertyName("college")]
        public EspnLinkDto? College { get; set; }

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        [JsonPropertyName("headshot")]
        public EspnAthleteHeadshot? Headshot { get; set; }

        [JsonPropertyName("injuries")]
        public List<object>? Injuries { get; set; }

        [JsonPropertyName("linked")]
        public bool Linked { get; set; }

        [JsonPropertyName("team")]
        public EspnLinkDto? Team { get; set; }

        [JsonPropertyName("teams")]
        public List<EspnLinkDto>? Teams { get; set; }

        [JsonPropertyName("statisticslog")]
        public EspnLinkDto? Statistics { get; set; }

        [JsonPropertyName("notes")]
        public EspnLinkDto? Notes { get; set; }

        [JsonPropertyName("experience")]
        public EspnAthleteExperience? Experience { get; set; }

        [JsonPropertyName("proAthlete")]
        public EspnLinkDto? ProAthlete { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("eventLog")]
        public EspnLinkDto? EventLog { get; set; }

        [JsonPropertyName("status")]
        public EspnAthleteStatusDto? Status { get; set; }
    }

    public class EspnAthleteExperience
    {
        [JsonPropertyName("years")]
        public int Years { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }
    }

    public class EspnAthleteHeadshot
    {
        [JsonPropertyName("href")]
        public Uri Href { get; set; }

        [JsonPropertyName("alt")]
        public string Alt { get; set; }
    }

#pragma warning restore CS8618 // Non-nullable property is uninitialized
}
