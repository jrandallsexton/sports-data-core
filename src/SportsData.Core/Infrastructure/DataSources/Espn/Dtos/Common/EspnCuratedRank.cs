using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

#pragma warning disable CS8618
public class EspnCuratedRank
{
    [JsonPropertyName("current")]
    public int Current { get; set; }
}