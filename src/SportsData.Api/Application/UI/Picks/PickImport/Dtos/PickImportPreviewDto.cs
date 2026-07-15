namespace SportsData.Api.Application.UI.Leagues.PickImport.Dtos;

/// <summary>
/// Dry-run plan for importing a user's picks from one league into another.
/// Returned by POST /ui/leagues/{targetId}/picks/import/preview — no writes.
/// </summary>
public sealed class PickImportPreviewDto
{
    public Guid SourceLeagueId { get; init; }

    public Guid TargetLeagueId { get; init; }

    /// <summary>
    /// When true, the FE must route the confirmed import through the
    /// (confidence-required) pick sheet rather than a direct commit — the
    /// imported team selections pre-fill the sheet and the user assigns
    /// confidence before saving.
    /// </summary>
    public bool TargetUsesConfidencePoints { get; init; }

    public List<PickImportPreviewItemDto> ToImport { get; init; } = [];

    public List<PickImportPreviewCollisionDto> Collisions { get; init; } = [];

    public List<PickImportPreviewSkippedDto> Skipped { get; init; } = [];
}

/// <summary>A contest whose source selection will be imported (no existing target pick).</summary>
public sealed class PickImportPreviewItemDto
{
    public Guid ContestId { get; init; }

    public int Week { get; init; }

    /// <summary>The picked team, copied verbatim from the source pick.</summary>
    public Guid FranchiseSeasonId { get; init; }

    public string? Headline { get; init; }

    /// <summary>
    /// The target league's locked spread this pick will be judged against — may
    /// differ from the source league's. Surfaced so the user understands the pick
    /// copies the team, not the line.
    /// </summary>
    public double? TargetHomeSpread { get; init; }
}

/// <summary>A contest where the existing target pick differs from the source selection.</summary>
public sealed class PickImportPreviewCollisionDto
{
    public Guid ContestId { get; init; }

    public int Week { get; init; }

    /// <summary>The team the source pick would import (replace with).</summary>
    public Guid SourceFranchiseSeasonId { get; init; }

    /// <summary>The team currently picked in the target league (keep).</summary>
    public Guid ExistingFranchiseSeasonId { get; init; }

    public string? Headline { get; init; }

    public double? TargetHomeSpread { get; init; }
}

/// <summary>A contest that was not imported, with the reason.</summary>
public sealed class PickImportPreviewSkippedDto
{
    public Guid ContestId { get; init; }

    /// <summary>One of <see cref="Planner.PickImportSkipReason"/> as a string.</summary>
    public string Reason { get; init; } = null!;

    public string? Headline { get; init; }
}
