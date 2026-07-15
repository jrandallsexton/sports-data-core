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
}
