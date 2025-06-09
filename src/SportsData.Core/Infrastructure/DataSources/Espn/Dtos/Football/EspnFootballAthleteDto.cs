#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

using System.Text.Json.Serialization;

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