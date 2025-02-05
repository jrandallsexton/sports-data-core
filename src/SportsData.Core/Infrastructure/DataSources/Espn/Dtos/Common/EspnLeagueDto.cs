using Newtonsoft.Json;

using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnLeagueDto
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }

        public string Id { get; set; }

        public string Guid { get; set; }

        public string Uid { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Abbreviation { get; set; }

        public string ShortName { get; set; }

        public string Slug { get; set; }

        public bool IsTournament { get; set; }

        public EspnLeagueSeasonDto Season { get; set; }

        public EspnLinkDto Seasons { get; set; }

        public EspnLinkDto Franchises { get; set; }

        public EspnLinkDto Teams { get; set; }

        public Group Group { get; set; }

        public EspnLinkDto Groups { get; set; }

        public EspnLinkDto Events { get; set; }

        public EspnLinkDto Notes { get; set; }

        public EspnLinkDto Rankings { get; set; }

        public EspnLinkDto Draft { get; set; }

        public EspnLinkDto Awards { get; set; }

        public List<EspnLinkFullDto> Links { get; set; }

        public List<Logo> Logos { get; set; }

        public EspnLinkDto Athletes { get; set; }

        public Calendar Calendar { get; set; }

        public EspnLinkDto Transactions { get; set; }

        public EspnLinkDto Leaders { get; set; }

        public string Gender { get; set; }
    }

    public class Item
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }

        public string Id { get; set; }

        public int Type { get; set; }

        public string Name { get; set; }

        public string Abbreviation { get; set; }

        public int Year { get; set; }

        public string StartDate { get; set; }

        public string EndDate { get; set; }

        public bool HasGroups { get; set; }

        public bool HasStandings { get; set; }

        public bool HasLegs { get; set; }

        public EspnLinkDto Groups { get; set; }

        public EspnLinkDto Weeks { get; set; }

        public EspnLinkDto Corrections { get; set; }

        public string Slug { get; set; }

        public Week Week { get; set; }

        public EspnLinkDto Leaders { get; set; }
    }

    public class Logo
    {
        public string Href { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Alt { get; set; }
        public List<string> Rel { get; set; }
        public string LastUpdated { get; set; }
    }

    public class EspnLeagueSeasonDto
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }

        public int Year { get; set; }

        public string StartDate { get; set; }

        public string EndDate { get; set; }

        public string DisplayName { get; set; }

        public EspnLeagueSeasonType Type { get; set; }

        public EspnLeagueSeasonTypes Types { get; set; }

        public EspnLinkDto Rankings { get; set; }

        public EspnLinkDto PowerIndexes { get; set; }

        public EspnLinkDto PowerIndexLeaders { get; set; }

        public EspnLinkDto Coaches { get; set; }

        public EspnLinkDto Athletes { get; set; }

        public EspnLinkDto Awards { get; set; }

        public EspnLinkDto Futures { get; set; }

        public EspnLinkDto Leaders { get; set; }

    }

    public class EspnLeagueSeasonType
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }

        public string Id { get; set; }

        public int Type { get; set; }

        public string Name { get; set; }

        public string Abbreviation { get; set; }

        public int Year { get; set; }

        public string StartDate { get; set; }

        public string EndDate { get; set; }

        public bool HasGroups { get; set; }

        public bool HasStandings { get; set; }

        public bool HasLegs { get; set; }

        public EspnLinkDto Groups { get; set; }

        public Week Week { get; set; }

        public EspnLinkDto Weeks { get; set; }

        public EspnLinkDto Corrections { get; set; }

        public EspnLinkDto Leaders { get; set; }

        public string Slug { get; set; }
    }

    public class EspnLeagueSeasonTypes
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }

        public int Count { get; set; }

        public int PageIndex { get; set; }

        public int PageSize { get; set; }

        public int PageCount { get; set; }

        public List<EspnLeagueSeasonType> Items { get; set; }
    }

    public class Week
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }

        public int Number { get; set; }

        public string StartDate { get; set; }

        public string EndDate { get; set; }

        public string Text { get; set; }

        public EspnLinkDto Rankings { get; set; }
    }
}
