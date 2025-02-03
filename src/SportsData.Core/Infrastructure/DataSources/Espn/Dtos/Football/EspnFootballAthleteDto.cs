using Newtonsoft.Json;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

public class EspnFootballAthleteDto : EspnAthleteDto
{
    [JsonProperty("position")]
    public EspnAthletePositionDto Position { get; set; }

    [JsonProperty("jersey")]
    public int Jersey { get; set; }

    [JsonProperty("hand")]
    public EspnAthleteHandDto Hand { get; set; }

    [JsonProperty("draft")]
    public EspnAthleteDraftDto Draft { get; set; }
}