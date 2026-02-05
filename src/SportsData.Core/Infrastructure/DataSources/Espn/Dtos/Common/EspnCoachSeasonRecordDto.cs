#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// Represents a data transfer object (DTO) for an ESPN coach season record, containing details about a coach's performance
    /// during a specific season and related statistics.
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/2/coaches/4079704/record
    /// </summary>
    /// <remarks>This class is used to deserialize JSON data representing a coach's season record, including their
    /// identifier, name, type, summary, and associated statistics. It implements the <see cref="IHasRef"/> interface,
    /// which provides a reference URI for the record.</remarks>

    public class EspnCoachSeasonRecordDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; }

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("stats")]
        public List<EspnCoachRecordStat> Stats { get; set; }
    }
}
