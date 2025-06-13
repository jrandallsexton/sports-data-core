#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Converters;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

/// <summary>
/// Represents a data transfer object (DTO) for grouping ESPN data by season, including metadata, links, and related
/// entities.
/// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/types/3/groups/151
/// </summary>
/// <remarks>This class is used to encapsulate information about a specific grouping of data in the ESPN API, 
/// such as conferences, teams, standings, and associated metadata. It includes properties for identifying  the group,
/// linking to related resources, and providing additional descriptive information.</remarks>
public class EspnGroupBySeasonDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("uid")]
    public string Uid { get; set; }

    [JsonPropertyName("id")]
    [JsonConverter(typeof(ParseStringToLongConverter))]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }

    [JsonPropertyName("shortName")]
    public string ShortName { get; set; }

    [JsonPropertyName("midsizeName")]
    public string MidsizeName { get; set; }

    [JsonPropertyName("season")]
    public EspnLinkDto Season { get; set; }

    [JsonPropertyName("parent")]
    public EspnLinkDto Parent { get; set; }

    [JsonPropertyName("standings")]
    public EspnLinkDto Standings { get; set; }

    [JsonPropertyName("isConference")]
    public bool IsConference { get; set; }

    [JsonPropertyName("logos")]
    public List<EspnImageDto> Logos { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; }

    [JsonPropertyName("teams")]
    public EspnLinkDto Teams { get; set; }

    [JsonPropertyName("links")]
    public List<EspnLinkFullDto> Links { get; set; }
}