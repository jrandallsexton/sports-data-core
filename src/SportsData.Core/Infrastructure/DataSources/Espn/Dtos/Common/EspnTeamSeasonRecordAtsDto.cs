#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// Represents a paginated collection of team season records against the spread (ATS)  as retrieved from the ESPN
    /// API.
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2023/types/3/teams/99/ats
    /// </summary>
    /// <remarks>This DTO (Data Transfer Object) encapsulates metadata about the pagination,  such as the
    /// total number of items, the current page index, the page size,  and the total number of pages, along with the
    /// list of items for the current page.</remarks>
    public class EspnTeamSeasonRecordAtsDto
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
        public List<EspnTeamSeasonRecordAtsItemDto> Items { get; set; }
    }

    /// <summary>
    /// Represents a team's season record against the spread (ATS), including wins, losses, pushes, and the record type.
    /// </summary>
    /// <remarks>This DTO (Data Transfer Object) is used to encapsulate a team's performance metrics for a
    /// season in terms of ATS. The record includes the number of wins, losses, and pushes, as well as the type of
    /// record.</remarks>
    public class EspnTeamSeasonRecordAtsItemDto
    {
        [JsonPropertyName("wins")]
        public int Wins { get; set; }

        [JsonPropertyName("losses")]
        public int Losses { get; set; }

        [JsonPropertyName("pushes")]
        public int Pushes { get; set; }

        [JsonPropertyName("type")]
        public EspnTeamSeasonRecordAtsItemTypeDto Type { get; set; }
    }

    public class EspnTeamSeasonRecordAtsItemTypeDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }
    }
}