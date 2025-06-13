#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// Represents a paginated collection of season notes for an athlete, as retrieved from the ESPN API.
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/4426333/notes
    /// </summary>
    /// <remarks>This DTO (Data Transfer Object) is used to encapsulate metadata about pagination, such as the
    /// total count of items, the current page index, the page size, and the total number of pages, along with the
    /// collection of season notes.</remarks>
    public class EspnAthleteSeasonNotesDto
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
        public List<EspnAthleteSeasonNoteDto> Items { get; set; }
    }


}
