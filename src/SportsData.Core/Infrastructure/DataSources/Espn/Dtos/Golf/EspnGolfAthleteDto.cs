using Newtonsoft.Json;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Golf;

public class EspnGolfAthleteDto : EspnAthleteDto
{
    [JsonProperty("debutYear")]
    public int DebutYear { get; set; }

    [JsonProperty("turnedPro")]
    public int TurnedPro { get; set; }

    [JsonProperty("amateur")]
    public bool IsAmateur { get; set; }

    [JsonProperty("gender")]
    public string Gender { get; set; }

    [JsonProperty("hand")]
    public EspnAthleteHandDto Hand { get; set; }

    [JsonProperty("statisticslog")]
    public EspnLinkDto StatisticsLog { get; set; }

    [JsonProperty("seasons")]
    public EspnLinkDto Seasons { get; set; }
}