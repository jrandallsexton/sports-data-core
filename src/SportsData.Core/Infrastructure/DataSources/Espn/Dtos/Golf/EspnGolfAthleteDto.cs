using System.Text.Json.Serialization;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Golf
{
    public class EspnGolfAthleteDto : EspnAthleteDto
    {
        [JsonPropertyName("debutYear")]
        public int DebutYear { get; set; }

        [JsonPropertyName("turnedPro")]
        public int TurnedPro { get; set; }

        [JsonPropertyName("amateur")]
        public bool IsAmateur { get; set; }

        [JsonPropertyName("gender")]
        public string Gender { get; set; }

        [JsonPropertyName("hand")]
        public EspnAthleteHandDto Hand { get; set; }

        //[JsonPropertyName("statisticslog")]
        //public EspnLinkDto StatisticsLog { get; set; }

        [JsonPropertyName("seasons")]
        public EspnLinkDto Seasons { get; set; }
    }
}