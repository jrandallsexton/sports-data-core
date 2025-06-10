using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnLeagueSeasonType
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; }

    [JsonPropertyName("hasGroups")]
    public bool HasGroups { get; set; }

    [JsonPropertyName("hasStandings")]
    public bool HasStandings { get; set; }

    [JsonPropertyName("hasLegs")]
    public bool HasLegs { get; set; }

    [JsonPropertyName("groups")]
    public EspnLinkDto Groups { get; set; }

    [JsonPropertyName("week")]
    public Week Week { get; set; }

    [JsonPropertyName("weeks")]
    public EspnLinkDto Weeks { get; set; }

    [JsonPropertyName("corrections")]
    public EspnLinkDto Corrections { get; set; }

    [JsonPropertyName("leaders")]
    public EspnLinkDto Leaders { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; }
}