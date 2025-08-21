#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

/// <summary>
/// Mapped to: SeasonPoll
/// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/rankings/1?lang=en&region=us
/// </summary>
public class EspnFootballSeasonRankingDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("shortName")]
    public string ShortName { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("rankings")]
    public List<EspnLinkDto>? Rankings { get; set; }
}