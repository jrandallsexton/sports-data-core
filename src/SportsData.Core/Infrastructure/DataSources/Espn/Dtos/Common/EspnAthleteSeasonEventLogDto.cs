#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// Represents a data transfer object (DTO) for an athlete's season event log in the ESPN system.
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2023/athletes/4426333/eventlog
    /// </summary>
    /// <remarks>This class provides information about an athlete's season events, including a reference URI
    /// and a collection of event details. It is typically used to deserialize data from the ESPN API.</remarks>
    public class EspnAthleteSeasonEventLogDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("events")]
        public EspnAthleteSeasonEventLogEventsDto Events { get; set; }
    }

    public class EspnAthleteSeasonEventLogEventsDto
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
        public List<EspnAthleteSeasonEventLogEventItemDto> Items { get; set; }
    }

    public class EspnAthleteSeasonEventLogEventItemDto
    {
        [JsonPropertyName("event")]
        public EspnLinkDto Event { get; set; }

        [JsonPropertyName("competition")]
        public EspnLinkDto Competition { get; set; }

        [JsonPropertyName("teamId")]
        public string TeamId { get; set; }

        [JsonPropertyName("played")]
        public bool Played { get; set; }

        [JsonPropertyName("statistics")]
        public EspnLinkDto Statistics { get; set; }
    }
}