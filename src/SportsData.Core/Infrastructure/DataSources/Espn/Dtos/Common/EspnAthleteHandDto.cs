using Newtonsoft.Json;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnAthleteHandDto
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonProperty("displayValue")]
        public string DisplayValue { get; set; }
    }
}
