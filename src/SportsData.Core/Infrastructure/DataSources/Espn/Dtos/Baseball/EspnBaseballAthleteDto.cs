#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball
{
    public class EspnBaseballAthleteDto : EspnAthleteDto
    {
        [JsonPropertyName("position")]
        public EspnAthletePositionDto Position { get; set; }

        [JsonPropertyName("jersey")]
        public string Jersey { get; set; }

        [JsonPropertyName("draft")]
        public EspnAthleteDraftDto Draft { get; set; }

        [JsonPropertyName("bats")]
        public EspnAthleteHandDto Bats { get; set; }

        [JsonPropertyName("throws")]
        public EspnAthleteHandDto Throws { get; set; }
    }
}
