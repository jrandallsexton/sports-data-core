using Newtonsoft.Json;
using SportsData.Core.Converters;

namespace SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Award
{
    public class Award
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }

        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("history")]
        public string History { get; set; }

        [JsonProperty("season")]
        public Item Season { get; set; }

        [JsonProperty("winners")]
        public List<Winner> Winners { get; set; }

        [JsonProperty("links")]
        public List<Link> Links { get; set; }
    }

    public class Link
    {
        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("rel")]
        public List<string> Rel { get; set; }

        [JsonProperty("href")]
        public Uri Href { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("shortText")]
        public string ShortText { get; set; }

        [JsonProperty("isExternal")]
        public bool IsExternal { get; set; }

        [JsonProperty("isPremium")]
        public bool IsPremium { get; set; }
    }

    public class Item
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }
    }

    public class Winner
    {
        [JsonProperty("athlete")]
        public Item Athlete { get; set; }

        [JsonProperty("team")]
        public Item Team { get; set; }
    }
}
