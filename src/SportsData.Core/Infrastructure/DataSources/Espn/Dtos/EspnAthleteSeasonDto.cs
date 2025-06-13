#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    /// <summary>
    /// Source: http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/4426333
    /// </summary>
    public class EspnAthleteSeasonDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("uid")]
        public string Uid { get; set; }

        [JsonPropertyName("guid")]
        public string Guid { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("alternateIds")]
        public EspnAlternateIdDto AlternateIds { get; set; }

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string LastName { get; set; }

        [JsonPropertyName("fullName")]
        public string FullName { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; }

        [JsonPropertyName("weight")]
        public double Weight { get; set; }

        [JsonPropertyName("displayWeight")]
        public string DisplayWeight { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }

        [JsonPropertyName("displayHeight")]
        public string DisplayHeight { get; set; }

        [JsonPropertyName("links")]
        public List<EspnLinkFullDto> Links { get; set; }

        [JsonPropertyName("birthPlace")]
        public EspnAddressDto BirthPlace { get; set; }

        [JsonPropertyName("birthCountry")]
        public EspnCountryDto BirthCountry { get; set; }

        [JsonPropertyName("college")]
        public EspnLinkDto College { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("headshot")]
        public EspnImageDto Headshot { get; set; }

        [JsonPropertyName("jersey")]
        public string Jersey { get; set; }

        [JsonPropertyName("flag")]
        public EspnImageDto Flag { get; set; }

        [JsonPropertyName("position")]
        public EspnAthletePositionDto Position { get; set; }

        [JsonPropertyName("injuries")]
        public List<object> Injuries { get; set; }

        [JsonPropertyName("linked")]
        public bool Linked { get; set; }

        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }

        [JsonPropertyName("teams")]
        public List<EspnLinkDto> Teams { get; set; }

        [JsonPropertyName("statistics")]
        public EspnLinkDto Statistics { get; set; }

        [JsonPropertyName("notes")]
        public EspnLinkDto Notes { get; set; }

        [JsonPropertyName("experience")]
        public EspnAthleteExperienceDto Experience { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("eventLog")]
        public EspnLinkDto EventLog { get; set; }

        [JsonPropertyName("status")]
        public EspnAthleteStatusDto Status { get; set; }
    }
}