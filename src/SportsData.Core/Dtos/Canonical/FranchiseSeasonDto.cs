using System;

namespace SportsData.Core.Dtos.Canonical;

public record FranchiseSeasonDto : DtoBase
{
    public Guid FranchiseId { get; init; }

    public int SeasonYear { get; init; }

    public string Slug { get; init; } = null!;

    public string Location { get; init; } = null!;

    public string Name { get; init; } = null!;

    public string Abbreviation { get; init; } = null!;

    public string DisplayName { get; init; } = null!;

    public string DisplayNameShort { get; init; } = null!;

    public string ColorCodeHex { get; init; } = null!;

    public string? ColorCodeAltHex { get; init; }

    public bool IsActive { get; init; }

    // Enrichment fields
    public int Wins { get; init; }

    public int Losses { get; init; }

    public int Ties { get; init; }

    public int ConferenceWins { get; init; }

    public int ConferenceLosses { get; init; }

    public int ConferenceTies { get; init; }
}
