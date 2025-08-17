#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// Represents an event in the ESPN data model, including details such as the event's identifier, name, date,
    /// associated season, competitions, and related links.
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334
    /// </summary>
    /// <remarks>This class is used to deserialize event data from ESPN's API. It provides properties for
    /// accessing key information about the event, such as its unique identifier, name, and associated metadata like
    /// season, league, and venue details. The event may also include links to related resources and a list of
    /// competitions associated with the event.</remarks>
    public class EspnEventDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("uid")]
        public string Uid { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; }

        [JsonPropertyName("season")]
        public EspnLinkDto Season { get; set; }

        [JsonPropertyName("seasonType")]
        public EspnLinkDto SeasonType { get; set; }

        [JsonPropertyName("week")]
        public EspnLinkDto? Week { get; set; }

        [JsonPropertyName("timeValid")]
        public bool TimeValid { get; set; }

        [JsonPropertyName("competitions")]
        public List<EspnEventCompetitionDto> Competitions { get; set; }

        [JsonPropertyName("links")]
        public List<EspnLinkFullDto> Links { get; set; }

        [JsonPropertyName("venues")]
        public List<EspnLinkDto> Venues { get; set; } = [];

        [JsonPropertyName("league")]
        public EspnLinkDto League { get; set; }
    }
}