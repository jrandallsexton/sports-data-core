#pragma warning disable CS8618

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnEventCompetitionSituationDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("lastPlay")]
        public EspnLinkDto? LastPlay { get; set; }

        [JsonPropertyName("down")]
        public int Down { get; set; }

        [JsonPropertyName("yardLine")]
        public int YardLine { get; set; }

        [JsonPropertyName("distance")]
        public int Distance { get; set; }

        [JsonPropertyName("isRedZone")]
        public bool IsRedZone { get; set; }

        [JsonPropertyName("homeTimeouts")]
        public int HomeTimeouts { get; set; }

        [JsonPropertyName("awayTimeouts")]
        public int AwayTimeouts { get; set; }
    }
}
