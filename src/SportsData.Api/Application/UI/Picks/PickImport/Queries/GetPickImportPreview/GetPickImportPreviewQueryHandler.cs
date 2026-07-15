using SportsData.Api.Application.UI.Picks.PickImport.Dtos;
using SportsData.Api.Application.UI.Picks.PickImport.Planner;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Picks.PickImport.Queries.GetPickImportPreview;

public interface IGetPickImportPreviewQueryHandler
{
    Task<Result<PickImportPreviewDto>> ExecuteAsync(
        GetPickImportPreviewQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Dry-run: classifies the target league's matchups against the user's source
/// picks and existing target picks. No writes. See
/// docs/features/pick-import-across-leagues.md.
/// </summary>
public class GetPickImportPreviewQueryHandler : IGetPickImportPreviewQueryHandler
{
    private readonly IPickImportPlanService _planService;

    public GetPickImportPreviewQueryHandler(IPickImportPlanService planService)
    {
        _planService = planService;
    }

    public async Task<Result<PickImportPreviewDto>> ExecuteAsync(
        GetPickImportPreviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var planResult = await _planService.BuildAsync(
            query.UserId, query.SourceLeagueId, query.TargetLeagueId, cancellationToken);

        if (planResult is not Success<PickImportPlanContext> success)
        {
            var failure = (Failure<PickImportPlanContext>)planResult;
            return new Failure<PickImportPreviewDto>(default!, failure.Status, failure.Errors);
        }

        var context = success.Value;
        var plan = context.Plan;

        var dto = new PickImportPreviewDto
        {
            SourceLeagueId = context.SourceLeagueId,
            TargetLeagueId = context.TargetLeagueId,
            TargetUsesConfidencePoints = context.TargetUsesConfidencePoints,
            ToImport = plan.ToImport
                .Select(i => new PickImportPreviewItemDto
                {
                    ContestId = i.ContestId,
                    Week = i.Week,
                    FranchiseSeasonId = i.FranchiseSeasonId,
                    Headline = i.Headline,
                    TargetHomeSpread = i.TargetHomeSpread
                })
                .ToList(),
            Collisions = plan.Collisions
                .Select(c => new PickImportPreviewCollisionDto
                {
                    ContestId = c.ContestId,
                    Week = c.Week,
                    SourceFranchiseSeasonId = c.SourceFranchiseSeasonId,
                    ExistingFranchiseSeasonId = c.ExistingFranchiseSeasonId,
                    Headline = c.Headline,
                    TargetHomeSpread = c.TargetHomeSpread
                })
                .ToList(),
            Skipped = plan.Skipped
                .Select(s => new PickImportPreviewSkippedDto
                {
                    ContestId = s.ContestId,
                    Reason = s.Reason.ToString(),
                    Headline = s.Headline
                })
                .ToList()
        };

        return new Success<PickImportPreviewDto>(dto);
    }
}
