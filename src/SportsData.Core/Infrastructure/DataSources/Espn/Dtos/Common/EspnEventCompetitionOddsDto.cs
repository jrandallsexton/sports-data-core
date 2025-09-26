#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

public class EspnEventCompetitionOddsDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("provider")]
    public EspnEventCompetitionOddsProvider Provider { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonPropertyName("overUnder")]
    public decimal? OverUnder { get; set; }

    [JsonPropertyName("spread")]
    public decimal? Spread { get; set; }

    [JsonPropertyName("overOdds")]
    public decimal? OverOdds { get; set; }

    [JsonPropertyName("underOdds")]
    public decimal? UnderOdds { get; set; }

    [JsonPropertyName("awayTeamOdds")]
    public EspnEventCompetitionOddsTeamOdds AwayTeamOdds { get; set; }

    [JsonPropertyName("homeTeamOdds")]
    public EspnEventCompetitionOddsTeamOdds HomeTeamOdds { get; set; }

    [JsonPropertyName("links")]
    public List<EspnLinkFullDto>? Links { get; set; }

    [JsonPropertyName("moneylineWinner")]
    public bool? MoneylineWinner { get; set; }

    [JsonPropertyName("spreadWinner")]
    public bool? SpreadWinner { get; set; }

    [JsonPropertyName("open")]
    public OddsPhaseBlock? Open { get; set; }

    [JsonPropertyName("close")]
    public OddsPhaseBlock? Close { get; set; }

    [JsonPropertyName("current")]
    public OddsPhaseBlock? Current { get; set; }

    [JsonPropertyName("propBets")]
    public EspnLinkDto? PropBets { get; set; }
}

public class EspnEventCompetitionOddsTeamOdds
{
    [JsonPropertyName("favorite")]
    public bool? Favorite { get; set; }

    [JsonPropertyName("underdog")]
    public bool? Underdog { get; set; }

    [JsonPropertyName("moneyLine")]
    public int? MoneyLine { get; set; }

    [JsonPropertyName("spreadOdds")]
    public decimal? SpreadOdds { get; set; }

    [JsonPropertyName("open")]
    public OddsPhaseBlock? Open { get; set; }

    [JsonPropertyName("close")]
    public OddsPhaseBlock? Close { get; set; }

    [JsonPropertyName("current")]
    public OddsPhaseBlock? Current { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto Team { get; set; }
}

public class OddsPhaseBlock
{
    [JsonPropertyName("favorite")]
    public bool? Favorite { get; set; } // appears in team open

    [JsonPropertyName("pointSpread")]
    public PriceBlock? PointSpread { get; set; }

    [JsonPropertyName("spread")]
    public PriceBlock? Spread { get; set; }

    [JsonPropertyName("moneyLine")]
    public PriceBlock? MoneyLine { get; set; }

    [JsonPropertyName("over")]
    public PriceBlock? Over { get; set; }

    [JsonPropertyName("under")]
    public PriceBlock? Under { get; set; }

    [JsonPropertyName("total")]
    public PriceBlock? Total { get; set; }
}

public class PriceBlock
{
    [JsonPropertyName("value")]
    public decimal? Value { get; set; }

    [JsonPropertyName("displayValue")]
    public string? DisplayValue { get; set; }

    [JsonPropertyName("alternateDisplayValue")]
    public string? AlternateDisplayValue { get; set; }

    [JsonPropertyName("decimal")]
    public decimal? Decimal { get; set; }

    [JsonPropertyName("fraction")]
    public string? Fraction { get; set; }

    [JsonPropertyName("american")]
    public string? American { get; set; }

    [JsonPropertyName("outcome")]
    public Outcome? Outcome { get; set; }
}

public class Outcome
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class EspnEventCompetitionOddsProvider
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}