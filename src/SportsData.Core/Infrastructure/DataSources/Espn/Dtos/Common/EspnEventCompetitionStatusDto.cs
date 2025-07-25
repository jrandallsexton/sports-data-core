#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// Represents the status of a competition within an ESPN event, including details such as the current period,
    /// clock, and type.
    /// https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/status
    /// </summary>
    /// <remarks>This class is used to encapsulate the status information of a competition in an ESPN event.
    /// It includes properties for tracking the current period, the remaining time on the clock, and the type of
    /// status.</remarks>
    public class EspnEventCompetitionStatusDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("clock")]
        public double Clock { get; set; }

        [JsonPropertyName("displayClock")]
        public string DisplayClock { get; set; }

        [JsonPropertyName("period")]
        public int Period { get; set; }

        [JsonPropertyName("type")]
        public EspnEventCompetitionStatusTypeDto Type { get; set; }
    }

    public class EspnEventCompetitionStatusTypeDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("detail")]
        public string Detail { get; set; }

        [JsonPropertyName("shortDetail")]
        public string ShortDetail { get; set; }
    }


}
