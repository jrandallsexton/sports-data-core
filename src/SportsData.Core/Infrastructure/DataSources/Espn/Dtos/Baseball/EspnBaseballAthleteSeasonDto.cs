#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball
{
    public class EspnBaseballAthleteSeasonDto : EspnAthleteSeasonDto
    {
        [JsonPropertyName("hotZones")]
        public List<EspnHotZoneDto> HotZones { get; set; }

        [JsonPropertyName("bats")]
        public EspnAthleteHandDto Bats { get; set; }

        [JsonPropertyName("throws")]
        public EspnAthleteHandDto Throws { get; set; }
    }

    public class EspnHotZoneDto
    {
        [JsonPropertyName("configurationId")]
        public int ConfigurationId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("splitTypeId")]
        public int SplitTypeId { get; set; }

        [JsonPropertyName("season")]
        public int Season { get; set; }

        [JsonPropertyName("seasonType")]
        public int SeasonType { get; set; }

        [JsonPropertyName("zones")]
        public List<EspnHotZoneEntryDto> Zones { get; set; }
    }

    public class EspnHotZoneEntryDto
    {
        [JsonPropertyName("zoneId")]
        public int ZoneId { get; set; }

        [JsonPropertyName("xMin")]
        public double XMin { get; set; }

        [JsonPropertyName("xMax")]
        public double XMax { get; set; }

        [JsonPropertyName("yMin")]
        public double YMin { get; set; }

        [JsonPropertyName("yMax")]
        public double YMax { get; set; }

        [JsonPropertyName("atBats")]
        public int? AtBats { get; set; }

        [JsonPropertyName("hits")]
        public int? Hits { get; set; }

        [JsonPropertyName("battingAvg")]
        public double? BattingAvg { get; set; }

        [JsonPropertyName("battingAvgScore")]
        public double? BattingAvgScore { get; set; }

        [JsonPropertyName("slugging")]
        public double? Slugging { get; set; }

        [JsonPropertyName("sluggingScore")]
        public double? SluggingScore { get; set; }
    }
}
