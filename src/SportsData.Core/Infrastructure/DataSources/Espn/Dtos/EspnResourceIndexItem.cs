#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Converters;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

public class EspnResourceIndexItem : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonConverter(typeof(ParseStringToIntConverter))]
    public int Id { get; set; }
}