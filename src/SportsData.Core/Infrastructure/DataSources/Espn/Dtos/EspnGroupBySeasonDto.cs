using SportsData.Core.Converters;

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnGroupBySeasonDto
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; }

        [JsonPropertyName("uid")]
        public string Uid { get; set; }

        [JsonPropertyName("id")]
        [JsonConverter(typeof(ParseStringToLongConverter))]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; }

        [JsonPropertyName("midsizeName")]
        public string MidsizeName { get; set; }

        [JsonPropertyName("season")]
        public EspnGroupBySeasonSeason Season { get; set; }

        [JsonPropertyName("parent")]
        public EspnGroupBySeasonParent Parent { get; set; }

        [JsonPropertyName("standings")]
        public EspnGroupBySeasonStandings Standings { get; set; }

        [JsonPropertyName("isConference")]
        public bool IsConference { get; set; }

        [JsonPropertyName("logos")]
        public List<Logo> Logos { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("teams")]
        public EspnGroupBySeasonTeams Teams { get; set; }

        [JsonPropertyName("links")]
        public List<EspnGroupBySeasonLink> Links { get; set; }

        // TODO: Review all of these removals and determine if they are needed for other purposes
        public class EspnGroupBySeasonSeason
        {
            // $ref removed
        }

        public class EspnGroupBySeasonParent
        {
            // $ref removed
        }

        public class EspnGroupBySeasonStandings
        {
            // $ref removed
        }

        public class Logo
        {
            [JsonPropertyName("href")]
            public string Href { get; set; }
        }

        public class EspnGroupBySeasonTeams
        {
            // $ref removed
        }

        public class EspnGroupBySeasonLink
        {
            [JsonPropertyName("language")]
            public string Language { get; set; }

            [JsonPropertyName("rel")]
            public List<string> Rel { get; set; }

            [JsonPropertyName("href")]
            public string Href { get; set; }

            [JsonPropertyName("text")]
            public string Text { get; set; }

            [JsonPropertyName("shortText")]
            public string ShortText { get; set; }

            [JsonPropertyName("isExternal")]
            public bool IsExternal { get; set; }

            [JsonPropertyName("isPremium")]
            public bool IsPremium { get; set; }
        }
    }
}
