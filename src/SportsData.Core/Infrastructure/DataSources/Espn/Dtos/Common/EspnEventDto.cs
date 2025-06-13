#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
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
        public EspnLinkDto Week { get; set; }

        [JsonPropertyName("timeValid")]
        public bool TimeValid { get; set; }

        [JsonPropertyName("competitions")]
        public List<EspnEventCompetitionDto> Competitions { get; set; }

        [JsonPropertyName("links")]
        public List<EspnLinkFullDto> Links { get; set; }

        [JsonPropertyName("venues")]
        public List<EspnVenueDto> Venues { get; set; }

        [JsonPropertyName("league")]
        public EspnLinkDto League { get; set; }
    }
}