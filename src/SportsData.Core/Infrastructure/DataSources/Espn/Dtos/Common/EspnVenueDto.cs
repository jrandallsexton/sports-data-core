﻿#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Converters;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

public class EspnVenueDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    [JsonConverter(typeof(ParseStringToLongConverter))] // Assumes you have or will rewrite this for System.Text.Json
    public long Id { get; set; }

    [JsonPropertyName("fullName")]
    public string FullName { get; set; }

    [JsonPropertyName("shortName")]
    public string ShortName { get; set; }

    [JsonPropertyName("address")]
    public EspnAddressDto Address { get; set; }

    [JsonPropertyName("capacity")]
    public int Capacity { get; set; }

    [JsonPropertyName("grass")]
    public bool Grass { get; set; }

    [JsonPropertyName("indoor")]
    public bool Indoor { get; set; }

    [JsonPropertyName("images")]
    public List<EspnImageDto> Images { get; set; }
}