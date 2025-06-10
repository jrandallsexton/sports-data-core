using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnLeagueSeasonDto
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    [JsonPropertyName("type")]
    public EspnLeagueSeasonType Type { get; set; }

    [JsonPropertyName("types")]
    public EspnLeagueSeasonTypes Types { get; set; }

    [JsonPropertyName("rankings")]
    public EspnLinkDto Rankings { get; set; }

    [JsonPropertyName("powerIndexes")]
    public EspnLinkDto PowerIndexes { get; set; }

    [JsonPropertyName("powerIndexLeaders")]
    public EspnLinkDto PowerIndexLeaders { get; set; }

    [JsonPropertyName("coaches")]
    public EspnLinkDto Coaches { get; set; }

    [JsonPropertyName("athletes")]
    public EspnLinkDto Athletes { get; set; }

    [JsonPropertyName("awards")]
    public EspnLinkDto Awards { get; set; }

    [JsonPropertyName("futures")]
    public EspnLinkDto Futures { get; set; }

    [JsonPropertyName("leaders")]
    public EspnLinkDto Leaders { get; set; }
}