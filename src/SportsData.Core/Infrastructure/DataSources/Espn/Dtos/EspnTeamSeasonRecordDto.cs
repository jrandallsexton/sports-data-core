#pragma warning disable CS8618 // Non-nullable property is uninitialized

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
public class EspnTeamSeasonRecordDto
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("pageIndex")]
    public int PageIndex { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }

    [JsonPropertyName("items")]
    public List<EspnTeamSeasonRecordItemDto> Items { get; set; }
}