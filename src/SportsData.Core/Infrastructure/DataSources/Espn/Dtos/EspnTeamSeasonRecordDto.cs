#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

/// <summary>
/// Represents a paginated collection of team season records retrieved from ESPN.
/// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/types/3/teams/99/record
/// </summary>
/// <remarks>This DTO (Data Transfer Object) is used to encapsulate information about a paginated result set,
/// including metadata such as the total count of items, the current page index, the page size, and the total number of
/// pages. The actual records are contained in the <see cref="Items"/> property.</remarks>
public class EspnTeamSeasonRecordDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    [JsonPropertyName("shortDisplayName")]
    public string ShortDisplayName { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; }

    [JsonPropertyName("displayValue")]
    public string DisplayValue { get; set; }

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("stats")]
    public List<EspnTeamSeasonRecordStatDto> Stats { get; set; }
}