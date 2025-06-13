#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/types/3/athletes/4426333/statistics
    /// </summary>
    public class EspnAthleteSeasonStatisticsDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("season")]
        public EspnLinkDto Season { get; set; }

        [JsonPropertyName("athlete")]
        public EspnLinkDto Athlete { get; set; }

        [JsonPropertyName("splits")]
        public EspnStatisticsSplitDto Splits { get; set; }

        [JsonPropertyName("seasonType")]
        public EspnLinkDto SeasonType { get; set; }
    }
}