using Newtonsoft.Json;

using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnGroupBySeasonDto
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }
        public string Uid { get; set; }
        public long Id { get; set; }
        public string Name { get; set; }
        public string Abbreviation { get; set; }
        public string ShortName { get; set; }
        public string MidsizeName { get; set; }
        public EspnGroupBySeasonSeason Season { get; set; }
        public EspnGroupBySeasonParent Parent { get; set; }
        public EspnGroupBySeasonStandings Standings { get; set; }
        public bool IsConference { get; set; }
        public List<Logo> Logos { get; set; }
        public string Slug { get; set; }
        public EspnGroupBySeasonTeams Teams { get; set; }
        public List<EspnGroupBySeasonLink> Links { get; set; }

        public class EspnGroupBySeasonSeason
        {
            [JsonProperty("$ref")]
            public string Ref { get; set; }
        }

        public class EspnGroupBySeasonParent
        {
            [JsonProperty("$ref")]
            public string Ref { get; set; }
        }

        public class EspnGroupBySeasonStandings
        {
            [JsonProperty("$ref")]
            public string Ref { get; set; }
        }

        public class Logo
        {
            public string Href { get; set; }
        }

        public class EspnGroupBySeasonTeams
        {
            [JsonProperty("$ref")]
            public string Ref { get; set; }
        }

        public class EspnGroupBySeasonLink
        {
            public string Language { get; set; }
            public List<string> Rel { get; set; }
            public string Href { get; set; }
            public string Text { get; set; }
            public string ShortText { get; set; }
            public bool IsExternal { get; set; }
            public bool IsPremium { get; set; }
        }
    }
}
