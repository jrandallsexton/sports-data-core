#pragma warning disable CS8618

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

public class EspnEventCompetitionDriveDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("sequenceNumber")]
    public string SequenceNumber { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto Team { get; set; }

    [JsonPropertyName("endTeam")]
    public EspnLinkDto EndTeam { get; set; }

    [JsonPropertyName("start")]
    public EspnEventCompetitionDriveStartDto Start { get; set; }

    [JsonPropertyName("end")]
    public EspnEventCompetitionDriveEndDto End { get; set; }

    [JsonPropertyName("timeElapsed")]
    public EspnEventCompetitionDriveTimeElapsedDto TimeElapsed { get; set; }

    [JsonPropertyName("yards")]
    public int Yards { get; set; }

    [JsonPropertyName("isScore")]
    public bool IsScore { get; set; }

    [JsonPropertyName("offensivePlays")]
    public int OffensivePlays { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; }

    [JsonPropertyName("shortDisplayResult")]
    public string ShortDisplayResult { get; set; }

    [JsonPropertyName("displayResult")]
    public string DisplayResult { get; set; }

    [JsonPropertyName("source")]
    public EspnEventCompetitionDriveSourceDto Source { get; set; }

    [JsonPropertyName("plays")]
    public EspnEventCompetitionPlaysDto Plays { get; set; }
}