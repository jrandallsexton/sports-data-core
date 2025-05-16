using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using SportsData.Core.Converters;

namespace SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Award
{
    public class Award
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(ParseStringToLongConverter))]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("history")]
        public string History { get; set; }

        [JsonPropertyName("season")]
        public Item Season { get; set; }

        [JsonPropertyName("winners")]
        public List<Winner> Winners { get; set; }

        [JsonPropertyName("links")]
        public List<Link> Links { get; set; }
    }

    public class Link
    {
        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("rel")]
        public List<string> Rel { get; set; }

        [JsonPropertyName("href")]
        public Uri Href { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("shortText")]
        public string ShortText { get; set; }

        [JsonPropertyName("isExternal")]
        public bool IsExternal { get; set; }

        [JsonPropertyName("isPremium")]
        public bool IsPremium { get; set; }
    }

    public class Item
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }
    }

    public class Winner
    {
        [JsonPropertyName("AthleteDto")]
        public Item Athlete { get; set; }

        [JsonPropertyName("team")]
        public Item Team { get; set; }
    }
}