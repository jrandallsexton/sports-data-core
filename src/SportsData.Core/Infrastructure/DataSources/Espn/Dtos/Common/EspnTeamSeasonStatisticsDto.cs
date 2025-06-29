
#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

public class EspnTeamSeasonStatisticsDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("season")]
    public EspnLinkDto Season { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto Team { get; set; }

    [JsonPropertyName("splits")]
    public EspnTeamSeasonStatisticsSplitsDto Splits { get; set; }

    [JsonPropertyName("seasonType")]
    public EspnLinkDto SeasonType { get; set; }
}

public class EspnTeamSeasonStatisticsCategoryDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    [JsonPropertyName("shortDisplayName")]
    public string ShortDisplayName { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; }

    [JsonPropertyName("stats")]
    public List<EspnTeamSeasonStatisticsCategoryStatDto> Stats { get; set; }
}

public class EspnTeamSeasonStatisticsSplitsDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }

    [JsonPropertyName("categories")]
    public List<EspnTeamSeasonStatisticsCategoryDto> Categories { get; set; }
}

public class EspnTeamSeasonStatisticsCategoryStatDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    [JsonPropertyName("shortDisplayName")]
    public string ShortDisplayName { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }

    [JsonPropertyName("value")]
    public decimal Value { get; set; }

    [JsonPropertyName("displayValue")]
    public string DisplayValue { get; set; }

    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("rankDisplayValue")]
    public string RankDisplayValue { get; set; }

    [JsonPropertyName("perGameValue")]
    public decimal? PerGameValue { get; set; }

    [JsonPropertyName("perGameDisplayValue")]
    public string PerGameDisplayValue { get; set; }
}