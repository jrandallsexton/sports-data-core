#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// Represents a data transfer object (DTO) for an ESPN coach, containing details such as personal information,
    /// career records, and coaching seasons.
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/559872
    /// </summary>
    /// <remarks>This class is used to deserialize JSON data from the ESPN API into a strongly-typed object.
    /// It includes properties for identifying the coach, their experience, and associated records or seasons.</remarks>
    public class EspnCoachDto : IHasRef
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

        [JsonPropertyName("dateOfBirth")]
        public DateTime? DateOfBirth { get; set; }

        [JsonPropertyName("experience")]
        public int Experience { get; set; }

        [JsonPropertyName("college")]
        public EspnLinkDto College { get; set; }

        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }

        [JsonPropertyName("careerRecords")]
        public List<EspnLinkDto> CareerRecords { get; set; }

        [JsonPropertyName("coachSeasons")]
        public List<EspnLinkDto> CoachSeasons { get; set; }
    }
}