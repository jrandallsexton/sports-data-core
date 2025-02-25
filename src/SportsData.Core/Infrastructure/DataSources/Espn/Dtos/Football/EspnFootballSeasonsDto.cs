using Newtonsoft.Json;

using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballSeasonsDto
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }

        public int Year { get; set; }

        public string StartDate { get; set; }

        public string EndDate { get; set; }

        public string DisplayName { get; set; }

        public EspnFootballSeasonsType Type { get; set; }

        public EspnFootballSeasonsTypes Types { get; set; }

        public EspnResourceIndexItem Rankings { get; set; }

        public EspnResourceIndexItem Athletes { get; set; }

        public EspnResourceIndexItem Awards { get; set; }

        public EspnResourceIndexItem Futures { get; set; }

        public EspnResourceIndexItem Leaders { get; set; }
    }

    public class EspnFootballSeasonsType
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
        public EspnResourceIndexItem Groups { get; set; }
        public EspnResourceIndexItem Weeks { get; set; }
        public string Slug { get; set; }
        public EspnResourceIndexItem Leaders { get; set; }
    }

    public class EspnFootballSeasonsTypes
    {
        public int Count { get; set; }
        public List<EspnFootballSeasonsType> Items { get; set; }
    }
}
