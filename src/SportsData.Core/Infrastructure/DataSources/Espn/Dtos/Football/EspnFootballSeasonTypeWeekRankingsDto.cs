#pragma warning disable CS8618

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballSeasonTypeWeekRankingsDto : IHasRef
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
        public EspnFootballSeasonTypeWeekRankingsOccurrence Occurrence { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("headline")]
        public string Headline { get; set; }

        [JsonPropertyName("shortHeadline")]
        public string ShortHeadline { get; set; }

        [JsonPropertyName("season")]
        public EspnFootballSeasonTypeWeekRankingsSeason Season { get; set; }

        [JsonPropertyName("lastUpdated")]
        public string LastUpdated { get; set; }

        [JsonPropertyName("ranks")]
        public List<EspnFootballSeasonTypeWeekRankingsRank> Ranks { get; set; }

        [JsonPropertyName("others")]
        public List<Other> Others { get; set; }

        [JsonPropertyName("availability")]
        public Availability Availability { get; set; }
    }

    public class Athletes
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; }
    }

    public class Availability
    {
    }

    public class Futures
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; }
    }

    public class Groups
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; }
    }

    public class EspnFootballSeasonTypeWeekRankingsSeasonTypesItem : IHasRef
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
        public Groups Groups { get; set; }

        [JsonPropertyName("week")]
        public EspnFootballSeasonTypeWeekRankingsSeasonTypesItemWeek Week { get; set; }

        [JsonPropertyName("weeks")]
        public EspnLinkDto Weeks { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }
    }

    public class EspnFootballSeasonTypeWeekRankingsOccurrence
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

    public class Other
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
        public EspnLinkDto Record { get; set; }

        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("lastUpdated")]
        public string LastUpdated { get; set; }
    }

    public class EspnFootballSeasonTypeWeekRankingsRank
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
        public EspnFootballSeasonTypeWeekRankingsRankRecord Record { get; set; }

        [JsonPropertyName("team")]
        public EspnLinkDto Team { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("lastUpdated")]
        public string LastUpdated { get; set; }
    }

    public class EspnFootballSeasonTypeWeekRankingsRankRecord
    {
        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("stats")]
        public List<EspnFootballSeasonTypeWeekRankingsRankRecordStat> Stats { get; set; }
    }

    public class EspnFootballSeasonTypeWeekRankingsSeason : IHasRef
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
        public EspnFootballSeasonTypeWeekRankingsSeasonType Type { get; set; }

        [JsonPropertyName("types")]
        public EspnFootballSeasonTypeWeekRankingsSeasonTypes Types { get; set; }

        [JsonPropertyName("rankings")]
        public EspnLinkDto Rankings { get; set; }

        [JsonPropertyName("athletes")]
        public Athletes Athletes { get; set; }

        [JsonPropertyName("futures")]
        public Futures Futures { get; set; }
    }

    public class EspnFootballSeasonTypeWeekRankingsRankRecordStat
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

    public class EspnFootballSeasonTypeWeekRankingsSeasonType
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
        public Groups Groups { get; set; }

        [JsonPropertyName("week")]
        public Week Week { get; set; }

        [JsonPropertyName("weeks")]
        public Weeks Weeks { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }
    }

    public class EspnFootballSeasonTypeWeekRankingsSeasonTypes : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("pageIndex")]
        public int PageIndex { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("pageCount")]
        public int PageCount { get; set; }

        [JsonPropertyName("items")]
        public List<EspnFootballSeasonTypeWeekRankingsSeasonTypesItem> Items { get; set; }
    }

    public class EspnFootballSeasonTypeWeekRankingsSeasonTypesItemWeek
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; }

        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("startDate")]
        public string StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string EndDate { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("rankings")]
        public EspnLinkDto Rankings { get; set; }
    }

    public class Weeks
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; }
    }


}
