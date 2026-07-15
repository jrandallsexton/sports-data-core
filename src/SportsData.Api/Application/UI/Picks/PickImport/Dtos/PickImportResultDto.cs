namespace SportsData.Api.Application.UI.Picks.PickImport.Dtos;

/// <summary>
/// Outcome of committing a cross-league pick import (non-confidence target).
/// Returned by POST /ui/leagues/{targetId}/picks/import.
/// </summary>
public sealed class PickImportResultDto
{
    /// <summary>New target picks created from source selections.</summary>
    public int Imported { get; init; }

    /// <summary>Existing target picks overwritten because the user chose to replace them.</summary>
    public int Replaced { get; init; }

    /// <summary>
    /// Contests not imported — plan skips (locked / already-matches / not-shared),
    /// collisions the user kept, and any per-contest submit failures.
    /// </summary>
    public int Skipped { get; init; }

    /// <summary>Skipped counts keyed by reason (e.g. Locked, AlreadyMatches, NotShared, KeptExisting, Failed).</summary>
    public Dictionary<string, int> SkippedByReason { get; init; } = new();

    /// <summary>
    /// True when the target uses confidence points: nothing was written. The
    /// selections in <see cref="Draft"/> pre-fill the pick sheet, and the user
    /// assigns a confidence value to each before saving via the normal
    /// (confidence-required) path.
    /// </summary>
    public bool RequiresConfidence { get; init; }

    /// <summary>
    /// Draft selections for a confidence target (import set plus approved replaces).
    /// Empty for a direct (non-confidence) commit.
    /// </summary>
    public List<PickImportDraftItemDto> Draft { get; init; } = [];
}

/// <summary>A team selection to pre-fill into a confidence-league pick sheet.</summary>
public sealed class PickImportDraftItemDto
{
    public Guid ContestId { get; init; }

    public int Week { get; init; }

    public Guid FranchiseSeasonId { get; init; }

    public string? Headline { get; init; }
}
