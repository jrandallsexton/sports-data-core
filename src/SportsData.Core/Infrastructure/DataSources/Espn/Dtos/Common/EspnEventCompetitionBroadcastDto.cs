#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary> 
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/broadcasts?lang=en
    /// </summary>
    public class EspnEventCompetitionBroadcastDto
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
        public List<EspnEventCompetitionBroadcastItem> Items { get; set; }
    }

    public class EspnEventCompetitionBroadcastItemMediaGroup
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }
    }

    /// <summary>
    /// Represents a broadcast item for an ESPN event competition, including details such as channel, station, and media
    /// information.
    /// </summary>
    /// <remarks>This class provides properties to access various attributes of a broadcast item, such as the
    /// type of broadcast, the channel number, the station name, and additional metadata like language and region. It
    /// also includes information about the competition and whether the broadcast is partnered.</remarks>
    public class EspnEventCompetitionBroadcastItem
    {
        [JsonPropertyName("type")]
        public EspnEventCompetitionBroadcastItemType Type { get; set; }

        [JsonPropertyName("channel")]
        public int Channel { get; set; }

        [JsonPropertyName("station")]
        public string Station { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        [JsonPropertyName("market")]
        public Market Market { get; set; }

        [JsonPropertyName("media")]
        public EspnEventCompetitionBroadcastItemMedia EspnEventCompetitionBroadcastItemMedia { get; set; }

        [JsonPropertyName("lang")]
        public string Lang { get; set; }

        [JsonPropertyName("region")]
        public string Region { get; set; }

        [JsonPropertyName("competition")]
        public EspnLinkDto Competition { get; set; }

        [JsonPropertyName("partnered")]
        public bool Partnered { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("stationKey")]
        public string StationKey { get; set; }
    }

    public class Logo
    {
        [JsonPropertyName("href")]
        public string Href { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("alt")]
        public string Alt { get; set; }

        [JsonPropertyName("rel")]
        public List<string> Rel { get; set; }

        [JsonPropertyName("lastUpdated")]
        public string LastUpdated { get; set; }
    }

    public class Market
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class EspnEventCompetitionBroadcastItemMedia : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("callLetters")]
        public string CallLetters { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("logos")]
        public List<Logo> Logos { get; set; }

        [JsonPropertyName("group")]
        public EspnEventCompetitionBroadcastItemMediaGroup Group { get; set; }
    }

    public class EspnEventCompetitionBroadcastItemType
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; }

        [JsonPropertyName("longName")]
        public string LongName { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }
    }
}
