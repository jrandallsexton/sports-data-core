#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

public class EspnResourceIndexItem : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    public string Id { get; set; }
}