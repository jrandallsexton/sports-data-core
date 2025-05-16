using System.Text.Json.Serialization;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballAthleteDto : EspnAthleteDto
    {
        [JsonPropertyName("position")]
        public EspnAthletePositionDto Position { get; set; }

        [JsonPropertyName("jersey")]
        public string Jersey { get; set; }

        [JsonPropertyName("hand")]
        public EspnAthleteHandDto Hand { get; set; }

        [JsonPropertyName("draft")]
        public EspnAthleteDraftDto Draft { get; set; }
    }
}