#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

/// <summary>
/// Mapped to: FranchiseSeasonRanking
/// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/types/2/weeks/16/teams/99/ranks/21?lang=en
/// </summary>
public class EspnTeamSeasonRankDto : IHasRef
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

    [JsonPropertyName("occurrence")]
    public EspnTeamSeasonRankOccurrence Occurrence { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonPropertyName("headline")]
    public string Headline { get; set; }

    [JsonPropertyName("shortHeadline")]
    public string ShortHeadline { get; set; }

    [JsonPropertyName("season")]
    public EspnTeamSeasonRankSeason Season { get; set; }

    [JsonPropertyName("defaultRanking")]
    public bool DefaultRanking { get; set; }

    [JsonPropertyName("lastUpdated")]
    public string LastUpdated { get; set; }

    [JsonPropertyName("notes")]
    public List<EspnTeamSeasonRankNote> Notes { get; set; }

    [JsonPropertyName("rank")]
    public EspnTeamSeasonRankDetail Rank { get; set; }
}

public class EspnTeamSeasonRankItem
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; }

    [JsonPropertyName("hasGroups")]
    public bool HasGroups { get; set; }

    [JsonPropertyName("hasStandings")]
    public bool HasStandings { get; set; }

    [JsonPropertyName("hasLegs")]
    public bool HasLegs { get; set; }

    [JsonPropertyName("groups")]
    public EspnLinkDto Groups { get; set; }

    [JsonPropertyName("weeks")]
    public EspnLinkDto Weeks { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; }

    [JsonPropertyName("leaders")]
    public EspnLinkDto Leaders { get; set; }
}

/// <summary>
/// Mapped to: FranchiseSeasonRankingNote
/// </summary>
public class EspnTeamSeasonRankNote
{
    [JsonPropertyName("text")]
    public string Text { get; set; }
}

/// <summary>
/// Mapped to: FranchiseSeasonRankingOccurrence
/// </summary>
public class EspnTeamSeasonRankOccurrence
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("last")]
    public bool Last { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("displayValue")]
    public string DisplayValue { get; set; }
}

/// <summary>
/// Mapped to: FranchiseSeasonRankingDetail
/// </summary>
public class EspnTeamSeasonRankDetail
{
    [JsonPropertyName("current")]
    public int Current { get; set; }

    [JsonPropertyName("previous")]
    public int Previous { get; set; }

    [JsonPropertyName("points")]
    public double Points { get; set; }

    [JsonPropertyName("firstPlaceVotes")]
    public int FirstPlaceVotes { get; set; }

    [JsonPropertyName("trend")]
    public string Trend { get; set; }

    [JsonPropertyName("record")]
    public EspnTeamSeasonRankDetailRecord Record { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto Team { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonPropertyName("lastUpdated")]
    public string LastUpdated { get; set; }
}

/// <summary>
/// Mapped to: FranchiseSeasonRankingDetailRecord
/// </summary>
public class EspnTeamSeasonRankDetailRecord
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; }

    [JsonPropertyName("stats")]
    public List<EspnTeamSeasonRankDetailRecordStat> Stats { get; set; }
}

public class EspnTeamSeasonRankSeason : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    [JsonPropertyName("type")]
    public EspnTeamSeasonRankSeasonType Type { get; set; }

    [JsonPropertyName("types")]
    public EspnTeamSeasonRankSeasonTypeTypes Types { get; set; }

    [JsonPropertyName("rankings")]
    public EspnLinkDto Rankings { get; set; }

    [JsonPropertyName("athletes")]
    public EspnLinkDto Athletes { get; set; }

    [JsonPropertyName("awards")]
    public EspnLinkDto Awards { get; set; }

    [JsonPropertyName("futures")]
    public EspnLinkDto Futures { get; set; }

    [JsonPropertyName("leaders")]
    public EspnLinkDto Leaders { get; set; }
}

/// <summary>
/// Mapped to: FranchiseSeasonRankingDetailRecordStat
/// </summary>
public class EspnTeamSeasonRankDetailRecordStat
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

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("displayValue")]
    public string DisplayValue { get; set; }
}

public class EspnTeamSeasonRankSeasonType : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; }

    [JsonPropertyName("hasGroups")]
    public bool HasGroups { get; set; }

    [JsonPropertyName("hasStandings")]
    public bool HasStandings { get; set; }

    [JsonPropertyName("hasLegs")]
    public bool HasLegs { get; set; }

    [JsonPropertyName("groups")]
    public EspnLinkDto Groups { get; set; }

    [JsonPropertyName("week")]
    public EspnLeagueSeasonWeek Week { get; set; }

    [JsonPropertyName("weeks")]
    public EspnLinkDto Weeks { get; set; }

    [JsonPropertyName("leaders")]
    public EspnLinkDto Leaders { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; }
}

public class EspnTeamSeasonRankSeasonTypeTypes
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("pageIndex")]
    public int PageIndex { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }

    [JsonPropertyName("items")]
    public List<EspnTeamSeasonRankItem> Items { get; set; }
}