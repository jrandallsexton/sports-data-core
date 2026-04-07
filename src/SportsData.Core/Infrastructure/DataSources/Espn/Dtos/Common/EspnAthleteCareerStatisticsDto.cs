#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

/// <summary>
/// http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/athletes/{id}/statistics
/// Career-aggregate statistics for an NFL athlete (as opposed to per-season statistics).
/// </summary>
public class EspnAthleteCareerStatisticsDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("athlete")]
    public EspnLinkDto Athlete { get; set; }

    [JsonPropertyName("splits")]
    public EspnStatisticsSplitDto Splits { get; set; }
}
