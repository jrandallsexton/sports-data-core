#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Converters;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

/// <summary>
/// Represents a team's season data as provided by ESPN, including identifiers, metadata, and related resources.
/// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99
/// </summary>
/// <remarks>This class encapsulates detailed information about a team's season, such as its unique
/// identifiers,  display properties, status, and links to related resources like statistics, records, and athletes.
/// It is typically used to deserialize data from ESPN's APIs.</remarks>
public class EspnTeamSeasonDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    [JsonConverter(typeof(ParseStringToLongConverter))]
    public long Id { get; set; }

    [JsonPropertyName("uid")]
    public string Uid { get; set; }

    [JsonPropertyName("alternateIds")]
    public EspnAlternateIdDto AlternateIds { get; set; }

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

    [JsonPropertyName("alternateColor")]
    public string AlternateColor { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("isAllStar")]
    public bool IsAllStar { get; set; }

    [JsonPropertyName("leaders")]
    public EspnLinkDto Leaders { get; set; }

    [JsonPropertyName("logos")] 
    public List<EspnImageDto> Logos { get; set; } = [];

    [JsonPropertyName("record")]
    public EspnLinkDto Record { get; set; }

    [JsonPropertyName("statistics")]
    public EspnLinkDto Statistics { get; set; }

    [JsonPropertyName("oddsRecords")]
    public EspnResourceIndexItem OddsRecords { get; set; }

    [JsonPropertyName("athletes")]
    public EspnResourceIndexItem Athletes { get; set; }

    [JsonPropertyName("venue")]
    public EspnVenueDto Venue { get; set; }

    [JsonPropertyName("groups")]
    public EspnLinkDto Groups { get; set; }

    [JsonPropertyName("ranks")]
    public EspnLinkDto Ranks { get; set; }

    [JsonPropertyName("links")]
    public List<EspnLinkFullDto> Links { get; set; }

    [JsonPropertyName("injuries")]
    public EspnLinkDto Injuries { get; set; }

    [JsonPropertyName("notes")]
    public EspnLinkDto Notes { get; set; }

    [JsonPropertyName("againstTheSpreadRecords")]
    public EspnLinkDto AgainstTheSpreadRecords { get; set; }

    [JsonPropertyName("awards")]
    public EspnLinkDto Awards { get; set; }

    [JsonPropertyName("franchise")]
    public EspnLinkDto Franchise { get; set; }

    [JsonPropertyName("projection")]
    public EspnResourceIndexItem Projection { get; set; }

    [JsonPropertyName("events")]
    public EspnLinkDto Events { get; set; }

    [JsonPropertyName("recruiting")]
    public EspnResourceIndexItem Recruiting { get; set; }

    [JsonPropertyName("college")]
    public EspnLinkDto College { get; set; }

    [JsonPropertyName("coaches")]
    public EspnLinkDto Coaches { get; set; }
}