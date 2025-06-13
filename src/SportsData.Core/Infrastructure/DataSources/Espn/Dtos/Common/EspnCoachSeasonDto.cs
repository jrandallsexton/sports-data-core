#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// Represents a coach's season data as provided by the ESPN API.
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2023/coaches/559872
    /// </summary>
    /// <remarks>This DTO (Data Transfer Object) contains information about a coach's identity, team,
    /// experience,  and performance records for a specific season. It is typically used to deserialize data from the 
    /// ESPN API and may include references to related entities such as the coach's personal details  and team
    /// information.</remarks>
    public class EspnCoachSeasonDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("uid")]
        public string Uid { get; set; }

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string LastName { get; set; }

        [JsonPropertyName("person")]
        public EspnLinkDto Person { get; set; }

        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }

        [JsonPropertyName("experience")]
        public int Experience { get; set; }

        [JsonPropertyName("records")]
        public List<EspnCoachSeasonRecordDto> Records { get; set; }
    }

    public class EspnCoachSeasonRecordDto
    {
        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }

        [JsonPropertyName("record")]
        public EspnLinkDto Record { get; set; }
    }
}