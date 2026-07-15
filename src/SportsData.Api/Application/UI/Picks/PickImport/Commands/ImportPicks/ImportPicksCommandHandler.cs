using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Picks.Commands.SubmitPick;
using SportsData.Api.Application.UI.Picks.PickImport.Dtos;
using SportsData.Api.Application.UI.Picks.PickImport.Planner;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Picks.PickImport.Commands.ImportPicks;

public interface IImportPicksCommandHandler
{
    Task<Result<PickImportResultDto>> ExecuteAsync(
        ImportPicksCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Commits a cross-league pick import into a non-confidence target: re-plans
/// server-side (never trusts the client's classification), then upserts exactly
/// the contests the user selected (<see cref="ImportPicksCommand.ContestIds"/>)
/// that the plan says are importable — a fresh import or a collision replace —
/// each via <see cref="ISubmitPickCommandHandler"/> so imported picks are
/// validated and published identically to hand-made ones. Unselected contests
/// are left untouched. See docs/features/pick-import-across-leagues.md.
/// </summary>
public class ImportPicksCommandHandler : IImportPicksCommandHandler
{
    private readonly ILogger<ImportPicksCommandHandler> _logger;
    private readonly IPickImportPlanService _planService;
    private readonly ISubmitPickCommandHandler _submitPick;

    public ImportPicksCommandHandler(
        ILogger<ImportPicksCommandHandler> logger,
        IPickImportPlanService planService,
        ISubmitPickCommandHandler submitPick)
    {
        _logger = logger;
        _planService = planService;
        _submitPick = submitPick;
    }

    public async Task<Result<PickImportResultDto>> ExecuteAsync(
        ImportPicksCommand command,
        CancellationToken cancellationToken = default)
    {
        var planResult = await _planService.BuildAsync(
            command.UserId, command.SourceLeagueId, command.TargetLeagueId, cancellationToken);

        if (planResult is not Success<PickImportPlanContext> success)
        {
            var failure = (Failure<PickImportPlanContext>)planResult;
            return new Failure<PickImportResultDto>(default!, failure.Status, failure.Errors);
        }

        var context = success.Value;

        var plan = context.Plan;
        // Null-safe: an absent/null selection (e.g. "contestIds": null in the
        // payload, which overrides the DTO default) means nothing was chosen —
        // import nothing rather than NRE.
        var selected = (command.ContestIds ?? Array.Empty<Guid>()).ToHashSet();

        // Confidence targets don't commit directly: the pick sheet is save-gated on
        // a confidence value per pick, so return the selected team selections as a
        // draft to pre-fill that sheet. No writes. See
        // docs/features/pick-import-across-leagues.md.
        if (context.TargetUsesConfidencePoints)
        {
            var draft = plan.ToImport
                .Where(i => selected.Contains(i.ContestId))
                .Select(i => new PickImportDraftItemDto
                {
                    ContestId = i.ContestId,
                    Week = i.Week,
                    FranchiseSeasonId = i.FranchiseSeasonId,
                    Headline = i.Headline
                })
                .Concat(plan.Collisions
                    .Where(c => selected.Contains(c.ContestId))
                    .Select(c => new PickImportDraftItemDto
                    {
                        ContestId = c.ContestId,
                        Week = c.Week,
                        FranchiseSeasonId = c.SourceFranchiseSeasonId,
                        Headline = c.Headline
                    }))
                .ToList();

            return new Success<PickImportResultDto>(new PickImportResultDto
            {
                RequiresConfidence = true,
                Draft = draft
            });
        }

        var imported = 0;
        var replaced = 0;
        var failed = 0;

        foreach (var item in plan.ToImport.Where(i => selected.Contains(i.ContestId)))
        {
            if (await TrySubmitAsync(command, context.TargetPickType, item.ContestId, item.Week,
                    item.FranchiseSeasonId, item.SourcePickId, cancellationToken))
                imported++;
            else
                failed++;
        }

        foreach (var collision in plan.Collisions.Where(c => selected.Contains(c.ContestId)))
        {
            if (await TrySubmitAsync(command, context.TargetPickType, collision.ContestId, collision.Week,
                    collision.SourceFranchiseSeasonId, collision.SourcePickId, cancellationToken))
                replaced++;
            else
                failed++;
        }

        // Importable contests the user left unchecked — left untouched, reported for transparency.
        var notSelected = plan.ToImport.Count(i => !selected.Contains(i.ContestId))
                          + plan.Collisions.Count(c => !selected.Contains(c.ContestId));

        var skippedByReason = plan.Skipped
            .GroupBy(s => s.Reason.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        if (notSelected > 0)
            skippedByReason["NotSelected"] = notSelected;
        if (failed > 0)
            skippedByReason["Failed"] = failed;

        var result = new PickImportResultDto
        {
            Imported = imported,
            Replaced = replaced,
            Skipped = plan.Skipped.Count + notSelected + failed,
            SkippedByReason = skippedByReason
        };

        _logger.LogInformation(
            "Pick import committed. UserId={UserId}, Source={Source}, Target={Target}, Imported={Imported}, Replaced={Replaced}, Skipped={Skipped}",
            command.UserId, command.SourceLeagueId, command.TargetLeagueId, imported, replaced, result.Skipped);

        return new Success<PickImportResultDto>(result);
    }

    private async Task<bool> TrySubmitAsync(
        ImportPicksCommand command,
        PickType targetPickType,
        Guid contestId,
        int week,
        Guid franchiseSeasonId,
        Guid sourcePickId,
        CancellationToken cancellationToken)
    {
        var submit = new SubmitPickCommand
        {
            UserId = command.UserId,
            PickemGroupId = command.TargetLeagueId,
            ContestId = contestId,
            Week = week,
            PickType = targetPickType,
            FranchiseSeasonId = franchiseSeasonId,
            ImportedFromPickId = sourcePickId
        };

        var result = await _submitPick.ExecuteAsync(submit, cancellationToken);
        if (result.IsSuccess)
            return true;

        // A contest can lock between plan and commit; treat as skip, not a hard
        // failure — re-running the import is idempotent and picks up the rest.
        _logger.LogWarning(
            "Pick import skipped a contest that failed to submit. UserId={UserId}, Target={Target}, ContestId={ContestId}, Status={Status}",
            command.UserId, command.TargetLeagueId, contestId, result.Status);
        return false;
    }
}
