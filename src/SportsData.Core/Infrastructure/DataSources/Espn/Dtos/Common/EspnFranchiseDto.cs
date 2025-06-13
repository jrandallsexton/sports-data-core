#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Converters;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

/// <summary>
/// Represents a franchise in the ESPN ecosystem, including its identifying information, branding, and related
/// resources.
/// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/99
/// </summary>
/// <remarks>This data transfer object (DTO) is used to encapsulate information about a sports franchise, such as
/// its name, location,  branding details, and associated resources like logos, venue, and team links. It is typically
/// used in scenarios where  franchise data is retrieved or transmitted via ESPN APIs.</remarks>
public class EspnFranchiseDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    [JsonConverter(typeof(ParseStringToLongConverter))]
    public long Id { get; set; }

    [JsonPropertyName("uid")]
    public string Uid { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; }

    [JsonPropertyName("location")]
    public string Location { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    [JsonPropertyName("shortDisplayName")]
    public string ShortDisplayName { get; set; }

    [JsonPropertyName("color")]
    public string Color { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("logos")]
    public List<EspnImageDto> Logos { get; set; }

    [JsonPropertyName("venue")]
    public EspnVenueDto? Venue { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto Team { get; set; }

    [JsonPropertyName("awards")]
    public EspnLinkDto Awards { get; set; }
}