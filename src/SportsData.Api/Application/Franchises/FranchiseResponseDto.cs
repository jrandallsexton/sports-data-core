using SportsData.Core.Dtos.Canonical;
using System;
using System.Collections.Generic;

namespace SportsData.Api.Application.Franchises;

/// <summary>
/// External API response DTO for franchise resources.
/// Enriched with HATEOAS refs and navigation links.
/// </summary>
public class FranchiseResponseDto
{
    public Guid Id { get; set; }

    public int Sport { get; set; }

    public required string Name { get; set; }

    public required string Nickname { get; set; }

    public required string Abbreviation { get; set; }

    public required string DisplayName { get; set; }

    public string? DisplayNameShort { get; set; }

    public required string ColorCodeHex { get; set; }

    public string? ColorCodeAltHex { get; set; }

    public required string Slug { get; set; }

    // HATEOAS properties
    public Uri Ref { get; set; } = null!;

    public Dictionary<string, Uri> Links { get; set; } = new();
}
