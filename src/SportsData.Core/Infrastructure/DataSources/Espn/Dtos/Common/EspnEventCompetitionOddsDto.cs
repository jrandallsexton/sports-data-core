#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnEventCompetitionOddsDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("provider")]
        public EspnEventCompetitionOddsProvider Provider { get; set; }

        [JsonPropertyName("details")]
        public string Details { get; set; }

        [JsonPropertyName("overUnder")]
        public decimal OverUnder { get; set; }

        [JsonPropertyName("spread")]
        public decimal Spread { get; set; }

        [JsonPropertyName("overOdds")]
        public decimal OverOdds { get; set; }

        [JsonPropertyName("underOdds")]
        public decimal UnderOdds { get; set; }

        [JsonPropertyName("awayTeamOdds")]
        public TeamOdds AwayTeamOdds { get; set; }

        [JsonPropertyName("homeTeamOdds")]
        public TeamOdds HomeTeamOdds { get; set; }

        [JsonPropertyName("links")]
        public List<EspnLinkFullDto> Links { get; set; }

        [JsonPropertyName("moneylineWinner")]
        public bool MoneylineWinner { get; set; }

        [JsonPropertyName("spreadWinner")]
        public bool SpreadWinner { get; set; }

        [JsonPropertyName("open")]
        public Open Open { get; set; }

        [JsonPropertyName("close")]
        public Close Close { get; set; }

        [JsonPropertyName("current")]
        public Current Current { get; set; }
    }

    public class TeamOdds
    {
        [JsonPropertyName("favorite")]
        public bool Favorite { get; set; }

        [JsonPropertyName("underdog")]
        public bool Underdog { get; set; }

        [JsonPropertyName("moneyLine")]
        public int MoneyLine { get; set; }

        [JsonPropertyName("spreadOdds")]
        public decimal SpreadOdds { get; set; }

        [JsonPropertyName("open")]
        public Open Open { get; set; }

        [JsonPropertyName("close")]
        public Close Close { get; set; }

        [JsonPropertyName("current")]
        public Current Current { get; set; }

        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }
    }

    public class Close
    {
        [JsonPropertyName("pointSpread")]
        public PointSpread PointSpread { get; set; }

        [JsonPropertyName("spread")]
        public Spread Spread { get; set; }

        [JsonPropertyName("moneyLine")]
        public MoneyLine MoneyLine { get; set; }

        [JsonPropertyName("over")]
        public Over Over { get; set; }

        [JsonPropertyName("under")]
        public Under Under { get; set; }

        [JsonPropertyName("total")]
        public Total Total { get; set; }
    }

    public class Current
    {
        [JsonPropertyName("pointSpread")]
        public PointSpread PointSpread { get; set; }

        [JsonPropertyName("spread")]
        public Spread Spread { get; set; }

        [JsonPropertyName("moneyLine")]
        public MoneyLine MoneyLine { get; set; }

        [JsonPropertyName("over")]
        public Over Over { get; set; }

        [JsonPropertyName("under")]
        public Under Under { get; set; }

        [JsonPropertyName("total")]
        public Total Total { get; set; }
    }

    public class MoneyLine
    {
        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; }

        [JsonPropertyName("alternateDisplayValue")]
        public string AlternateDisplayValue { get; set; }

        [JsonPropertyName("decimal")]
        public decimal Decimal { get; set; }

        [JsonPropertyName("fraction")]
        public string Fraction { get; set; }

        [JsonPropertyName("american")]
        public string American { get; set; }

        [JsonPropertyName("outcome")]
        public Outcome Outcome { get; set; }
    }

    public class Open
    {
        [JsonPropertyName("favorite")]
        public bool Favorite { get; set; }

        [JsonPropertyName("pointSpread")]
        public PointSpread PointSpread { get; set; }

        [JsonPropertyName("spread")]
        public Spread Spread { get; set; }

        [JsonPropertyName("moneyLine")]
        public MoneyLine MoneyLine { get; set; }

        [JsonPropertyName("over")]
        public Over Over { get; set; }

        [JsonPropertyName("under")]
        public Under Under { get; set; }

        [JsonPropertyName("total")]
        public Total Total { get; set; }
    }

    public class Outcome
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class Over
    {
        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; }

        [JsonPropertyName("alternateDisplayValue")]
        public string AlternateDisplayValue { get; set; }

        [JsonPropertyName("decimal")]
        public decimal Decimal { get; set; }

        [JsonPropertyName("fraction")]
        public string Fraction { get; set; }

        [JsonPropertyName("american")]
        public string American { get; set; }

        [JsonPropertyName("outcome")]
        public Outcome Outcome { get; set; }
    }

    public class PointSpread
    {
        [JsonPropertyName("alternateDisplayValue")]
        public string AlternateDisplayValue { get; set; }

        [JsonPropertyName("american")]
        public string American { get; set; }

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; }

        [JsonPropertyName("decimal")]
        public decimal Decimal { get; set; }

        [JsonPropertyName("fraction")]
        public string Fraction { get; set; }
    }

    public class EspnEventCompetitionOddsProvider
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; }
    }

    public class Spread
    {
        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; }

        [JsonPropertyName("alternateDisplayValue")]
        public string AlternateDisplayValue { get; set; }

        [JsonPropertyName("decimal")]
        public decimal Decimal { get; set; }

        [JsonPropertyName("fraction")]
        public string Fraction { get; set; }

        [JsonPropertyName("american")]
        public string American { get; set; }

        [JsonPropertyName("outcome")]
        public Outcome Outcome { get; set; }
    }

    public class Total
    {
        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; }

        [JsonPropertyName("alternateDisplayValue")]
        public string AlternateDisplayValue { get; set; }

        [JsonPropertyName("decimal")]
        public decimal Decimal { get; set; }

        [JsonPropertyName("fraction")]
        public string Fraction { get; set; }

        [JsonPropertyName("american")]
        public string American { get; set; }
    }

    public class Under
    {
        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; }

        [JsonPropertyName("alternateDisplayValue")]
        public string AlternateDisplayValue { get; set; }

        [JsonPropertyName("decimal")]
        public decimal Decimal { get; set; }

        [JsonPropertyName("fraction")]
        public string Fraction { get; set; }

        [JsonPropertyName("american")]
        public string American { get; set; }

        [JsonPropertyName("outcome")]
        public Outcome Outcome { get; set; }
    }


}
