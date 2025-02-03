using Newtonsoft.Json;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnAthleteBirthPlaceDto
    {
        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }
    }
}
