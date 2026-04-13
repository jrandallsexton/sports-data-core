#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Collections.Generic;
using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;

public class EspnBaseballEventCompetitionDto : EspnEventCompetitionDtoBase
{
    [JsonPropertyName("timeOfDay")]
    public string? TimeOfDay { get; set; }

    [JsonPropertyName("duration")]
    public EspnBaseballCompetitionDurationDto? Duration { get; set; }

    [JsonPropertyName("necessary")]
    public bool Necessary { get; set; }

    [JsonPropertyName("wasSuspended")]
    public bool WasSuspended { get; set; }

    [JsonPropertyName("seriesId")]
    public string? SeriesId { get; set; }

    [JsonPropertyName("series")]
    public List<EspnBaseballSeriesDto>? Series { get; set; }

    [JsonPropertyName("officials")]
    public EspnLinkDto? Officials { get; set; }

    [JsonPropertyName("relevancy")]
    public EspnLinkDto? Relevancy { get; set; }

    [JsonPropertyName("dataFormat")]
    public string? DataFormat { get; set; }
}

public class EspnBaseballCompetitionDurationDto
{
    [JsonPropertyName("displayValue")]
    public string? DisplayValue { get; set; }
}

public class EspnBaseballSeriesDto
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("totalCompetitions")]
    public int TotalCompetitions { get; set; }

    [JsonPropertyName("competitors")]
    public List<EspnBaseballSeriesCompetitorDto>? Competitors { get; set; }

    [JsonPropertyName("events")]
    public List<EspnLinkDto>? Events { get; set; }

    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }
}

public class EspnBaseballSeriesCompetitorDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("wins")]
    public int Wins { get; set; }

    [JsonPropertyName("ties")]
    public int Ties { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto? Team { get; set; }
}
