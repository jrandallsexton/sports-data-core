﻿#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

public class EspnEventCompetitionDriveTimeElapsedDto
{
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("displayValue")]
    public string DisplayValue { get; set; }
}