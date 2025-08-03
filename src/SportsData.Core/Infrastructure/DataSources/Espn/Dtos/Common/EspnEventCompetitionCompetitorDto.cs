#pragma warning disable CS8618

using System;
using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

public class EspnEventCompetitionCompetitorDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("uid")]
    public string Uid { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("homeAway")]
    public string HomeAway { get; set; }

    [JsonPropertyName("winner")]
    public bool Winner { get; set; }

    [JsonPropertyName("team")]
    public EspnLinkDto Team { get; set; }

    [JsonPropertyName("score")]
    public EspnLinkDto Score { get; set; }

    [JsonPropertyName("linescores")]
    public EspnLinkDto Linescores { get; set; }

    [JsonPropertyName("roster")]
    public EspnLinkDto Roster { get; set; }

    [JsonPropertyName("statistics")]
    public EspnLinkDto Statistics { get; set; }

    [JsonPropertyName("leaders")]
    public EspnLinkDto Leaders { get; set; }

    [JsonPropertyName("record")]
    public EspnLinkDto Record { get; set; }

    [JsonPropertyName("ranks")]
    public EspnLinkDto Ranks { get; set; }

    [JsonPropertyName("curatedRank")]
    public EspnCuratedRank EspnCuratedRank { get; set; }
}