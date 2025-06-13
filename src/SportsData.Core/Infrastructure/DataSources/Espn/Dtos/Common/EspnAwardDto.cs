#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// Represents an award in the ESPN system, including its metadata, history, and associated winners.
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2023/awards/3
    /// </summary>
    /// <remarks>This data transfer object (DTO) is used to encapsulate information about an ESPN award, such
    /// as its name, description, history, and related entities like the season and winners. It is typically used in
    /// scenarios where award details need to be retrieved or displayed.</remarks>
    public class EspnAwardDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("history")]
        public string History { get; set; }

        [JsonPropertyName("season")]
        public EspnLinkDto Season { get; set; }

        [JsonPropertyName("winners")]
        public List<WinnerDto> Winners { get; set; }

        [JsonPropertyName("links")]
        public List<EspnLinkFullDto> Links { get; set; }

        public class WinnerDto
        {
            [JsonPropertyName("athlete")]
            public EspnLinkDto Athlete { get; set; }

            [JsonPropertyName("team")]
            public EspnLinkDto Team { get; set; }
        }
    }
}