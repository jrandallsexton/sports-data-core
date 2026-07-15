namespace SportsData.Api.Application.UI.Leagues.PickImport.Planner;

/// <summary>
/// Pure, storage-agnostic classification core for cross-league pick import.
/// Given the target league's matchups plus the user's source and existing-target
/// selections, it classifies each target contest per the feature outcome table
/// (import / no-op / collision / skip). Both the preview (dry-run) and the commit
/// path build on this so their classification can never drift apart.
/// See docs/features/pick-import-across-leagues.md.
/// </summary>
public interface IPickImportPlanner
{
    PickImportPlan BuildPlan(PickImportPlanInput input);
}

public class PickImportPlanner : IPickImportPlanner
{
    public PickImportPlan BuildPlan(PickImportPlanInput input)
    {
        var plan = new PickImportPlan();

        foreach (var matchup in input.TargetMatchups)
        {
            // No source pick for this contest → nothing to copy.
            if (!input.SourceSelectionsByContest.TryGetValue(matchup.ContestId, out var source))
            {
                plan.Skipped.Add(new PickImportPlannedSkip(
                    matchup.ContestId, PickImportSkipReason.NotShared, matchup.Headline));
                continue;
            }

            // Target matchup already locked/started → cannot pick it.
            if (matchup.IsLocked)
            {
                plan.Skipped.Add(new PickImportPlannedSkip(
                    matchup.ContestId, PickImportSkipReason.Locked, matchup.Headline));
                continue;
            }

            if (input.ExistingTargetSelectionsByContest.TryGetValue(matchup.ContestId, out var existing))
            {
                if (existing == source.FranchiseSeasonId)
                {
                    // Already matches — silent no-op, surfaced only as informational.
                    plan.Skipped.Add(new PickImportPlannedSkip(
                        matchup.ContestId, PickImportSkipReason.AlreadyMatches, matchup.Headline));
                }
                else
                {
                    // Genuine conflict — user resolves keep/replace.
                    plan.Collisions.Add(new PickImportPlannedCollision(
                        matchup.ContestId, matchup.Week,
                        source.FranchiseSeasonId, existing, source.SourcePickId,
                        matchup.Headline, matchup.HomeSpread));
                }
            }
            else
            {
                plan.ToImport.Add(new PickImportPlannedImport(
                    matchup.ContestId, matchup.Week,
                    source.FranchiseSeasonId, source.SourcePickId,
                    matchup.Headline, matchup.HomeSpread));
            }
        }

        return plan;
    }
}

/// <summary>Inputs to <see cref="IPickImportPlanner.BuildPlan"/>, all in-memory.</summary>
public sealed record PickImportPlanInput(
    IReadOnlyCollection<PickImportTargetMatchup> TargetMatchups,
    IReadOnlyDictionary<Guid, PickImportSourceSelection> SourceSelectionsByContest,
    IReadOnlyDictionary<Guid, Guid> ExistingTargetSelectionsByContest);

/// <summary>A target-league matchup that defines the import universe.</summary>
public sealed record PickImportTargetMatchup(
    Guid ContestId, int Week, bool IsLocked, string? Headline, double? HomeSpread);

/// <summary>The user's source-league selection for a shared contest.</summary>
public sealed record PickImportSourceSelection(Guid FranchiseSeasonId, Guid SourcePickId);

public sealed class PickImportPlan
{
    public List<PickImportPlannedImport> ToImport { get; } = [];
    public List<PickImportPlannedCollision> Collisions { get; } = [];
    public List<PickImportPlannedSkip> Skipped { get; } = [];
}

public sealed record PickImportPlannedImport(
    Guid ContestId, int Week, Guid FranchiseSeasonId, Guid SourcePickId, string? Headline, double? TargetHomeSpread);

public sealed record PickImportPlannedCollision(
    Guid ContestId, int Week, Guid SourceFranchiseSeasonId, Guid ExistingFranchiseSeasonId, Guid SourcePickId, string? Headline, double? TargetHomeSpread);

public sealed record PickImportPlannedSkip(
    Guid ContestId, PickImportSkipReason Reason, string? Headline);

public enum PickImportSkipReason
{
    /// <summary>Target has no source pick for this contest (nothing to copy).</summary>
    NotShared,

    /// <summary>Target matchup has locked/started; cannot be picked.</summary>
    Locked,

    /// <summary>Existing target pick already equals the source selection.</summary>
    AlreadyMatches
}
