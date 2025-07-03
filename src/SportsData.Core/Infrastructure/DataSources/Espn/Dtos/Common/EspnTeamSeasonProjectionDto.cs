#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;
using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

/// <summary>
/// Represents team season projection data as provided by ESPN.
/// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/99/projection
/// </summary>
public class EspnTeamSeasonProjectionDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto Team { get; set; }

    [JsonPropertyName("chanceToWinDivision")]
    public decimal ChanceToWinDivision { get; set; }

    [JsonPropertyName("chanceToWinConference")]
    public decimal ChanceToWinConference { get; set; }

    [JsonPropertyName("projectedWins")]
    public decimal ProjectedWins { get; set; }

    [JsonPropertyName("projectedLosses")]
    public decimal ProjectedLosses { get; set; }
}
