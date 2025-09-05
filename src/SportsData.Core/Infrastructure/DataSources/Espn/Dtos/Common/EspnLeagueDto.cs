#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnLeagueDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("guid")]
        public string Guid { get; set; }

        [JsonPropertyName("uid")]
        public string Uid { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("isTournament")]
        public bool IsTournament { get; set; }

        [JsonPropertyName("season")]
        public EspnLeagueSeasonDto Season { get; set; }

        [JsonPropertyName("seasons")]
        public EspnLinkDto Seasons { get; set; }

        [JsonPropertyName("franchises")]
        public EspnLinkDto Franchises { get; set; }

        [JsonPropertyName("teams")]
        public EspnLinkDto Teams { get; set; }

        [JsonPropertyName("group")]
        public EspnLinkDto Group { get; set; }

        [JsonPropertyName("groups")]
        public EspnLinkDto Groups { get; set; }

        [JsonPropertyName("events")]
        public EspnLinkDto Events { get; set; }

        [JsonPropertyName("notes")]
        public EspnLinkDto Notes { get; set; }

        [JsonPropertyName("rankings")]
        public EspnLinkDto Rankings { get; set; }

        [JsonPropertyName("draft")]
        public EspnLinkDto Draft { get; set; }

        [JsonPropertyName("awards")]
        public EspnLinkDto Awards { get; set; }

        [JsonPropertyName("links")]
        public List<EspnLinkFullDto> Links { get; set; }

        [JsonPropertyName("logos")]
        public List<EspnImageDto> Logos { get; set; }

        [JsonPropertyName("athletes")]
        public EspnLinkDto Athletes { get; set; }

        [JsonPropertyName("calendar")]
        public EspnLinkDto Calendar { get; set; }

        [JsonPropertyName("transactions")]
        public EspnLinkDto Transactions { get; set; }

        [JsonPropertyName("leaders")]
        public EspnLinkDto Leaders { get; set; }

        [JsonPropertyName("gender")]
        public string Gender { get; set; }
    }

    public class EspnLeagueSeasonTypes
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("pageIndex")]
        public int PageIndex { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("pageCount")]
        public int PageCount { get; set; }

        [JsonPropertyName("items")]
        public List<EspnLeagueSeasonType> Items { get; set; }
    }

    public class EspnLeagueSeasonWeek : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

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

    public class Item
    {
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

        [JsonPropertyName("corrections")]
        public EspnLinkDto Corrections { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("week")]
        public EspnLeagueSeasonWeek Week { get; set; }

        [JsonPropertyName("leaders")]
        public EspnLinkDto Leaders { get; set; }
    }
}
