using Newtonsoft.Json;

using SportsData.Core.Converters;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

public class EspnAthleteDto
{
    [JsonProperty("$ref")]
    public Uri Ref { get; set; }

    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("uid")]
    public string Uid { get; set; }

    [JsonProperty("guid")]
    public string Guid { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("alternateIds")]
    public EspnAthleteAlternateIdsDto AlternateIds { get; set; }

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
    public decimal Height { get; set; }

    [JsonProperty("displayHeight")]
    public string DisplayHeight { get; set; }

    [JsonProperty("age")]
    public int Age { get; set; }

    [JsonProperty("dateOfBirth")]
    public string DateOfBirth { get; set; }

    [JsonProperty("links")]
    public List<EspnLinkFullDto> Links { get; set; }

    [JsonProperty("birthPlace")]
    public EspnAthleteBirthPlaceDto BirthPlace { get; set; }

    [JsonProperty("college")]
    public EspnLinkDto College { get; set; }

    [JsonProperty("slug")]
    public string Slug { get; set; }

    [JsonProperty("headshot")]
    public EspnAthleteHeadshot? Headshot { get; set; }

    [JsonProperty("injuries")]
    public List<object> Injuries { get; set; }

    [JsonProperty("linked")]
    public bool Linked { get; set; }

    [JsonProperty("team")]
    public EspnLinkDto Team { get; set; }

    [JsonProperty("teams")]
    public List<EspnLinkDto> Teams { get; set; }

    [JsonProperty("statisticslog")]
    public EspnLinkDto Statistics { get; set; }

    [JsonProperty("notes")]
    public EspnLinkDto Notes { get; set; }

    [JsonProperty("experience")]
    public EspnAthleteExperience Experience { get; set; }

    [JsonProperty("proAthlete")]
    public EspnLinkDto ProAthlete { get; set; }

    [JsonProperty("active")]
    public bool Active { get; set; }

    [JsonProperty("eventLog")]
    public EspnLinkDto EventLog { get; set; }

    [JsonProperty("status")]
    public EspnAthleteStatusDto Status { get; set; }
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