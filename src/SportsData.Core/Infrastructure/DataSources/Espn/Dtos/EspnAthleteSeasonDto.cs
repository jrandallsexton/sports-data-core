using Newtonsoft.Json;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnAthleteSeasonDto
    {
        [JsonProperty("$ref")]
        public Uri @ref { get; set; }

        public EspnAthleteSeasonSeason Season { get; set; }

        public EspnAthleteSeasonSplits Splits { get; set; }

        public EspnAthleteSeasonSeasonType SeasonType { get; set; }
    }

    public class EspnAthleteSeasonCategory
    {
        public string name { get; set; }
        public string displayName { get; set; }
        public string shortDisplayName { get; set; }
        public string abbreviation { get; set; }
        public string summary { get; set; }
        public List<EspnAthleteSeasonStat> stats { get; set; }
    }

    public class EspnAthleteSeasonSeason
    {
        [JsonProperty("$ref")]
        public string @ref { get; set; }
    }

    public class EspnAthleteSeasonSeasonType
    {
        [JsonProperty("$ref")]
        public string @ref { get; set; }
    }

    public class EspnAthleteSeasonSplits
    {
        public string id { get; set; }
        public string name { get; set; }
        public string abbreviation { get; set; }
        public string type { get; set; }
        public List<EspnAthleteSeasonCategory> categories { get; set; }
    }

    public class EspnAthleteSeasonStat
    {
        public string name { get; set; }
        public string displayName { get; set; }
        public string shortDisplayName { get; set; }
        public string description { get; set; }
        public string abbreviation { get; set; }
        public double value { get; set; }
        public string displayValue { get; set; }
        public double? perGameValue { get; set; }
        public string perGameDisplayValue { get; set; }
    }


}
