using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnAthleteSeasonDto
    {
        // $ref removed

        [JsonPropertyName("season")]
        public EspnAthleteSeasonSeason Season { get; set; }

        [JsonPropertyName("splits")]
        public EspnAthleteSeasonSplits Splits { get; set; }

        [JsonPropertyName("seasonType")]
        public EspnAthleteSeasonSeasonType SeasonType { get; set; }
    }

    public class EspnAthleteSeasonSeason
    {
        // $ref removed
    }

    public class EspnAthleteSeasonSeasonType
    {
        // $ref removed
    }

    public class EspnAthleteSeasonSplits
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("categories")]
        public List<EspnAthleteSeasonCategory> Categories { get; set; }
    }

    public class EspnAthleteSeasonCategory
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("shortDisplayName")]
        public string ShortDisplayName { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("stats")]
        public List<EspnAthleteSeasonStat> Stats { get; set; }
    }

    public class EspnAthleteSeasonStat
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

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; }

        [JsonPropertyName("perGameValue")]
        public double? PerGameValue { get; set; }

        [JsonPropertyName("perGameDisplayValue")]
        public string PerGameDisplayValue { get; set; }
    }
}
