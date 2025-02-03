using Newtonsoft.Json;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnAthleteDraftDto
    {
        [JsonProperty("displayText")]
        public string Display { get; set; }

        [JsonProperty("round")]
        public int Round { get; set; }

        [JsonProperty("year")]
        public int Year { get; set; }

        [JsonProperty("selection")]
        public int Selection { get; set; }

        [JsonProperty("team")]
        public EspnLinkDto Team { get; set; }

        [JsonProperty("pick")]
        public EspnLinkDto Pick { get; set; }
    }
}
