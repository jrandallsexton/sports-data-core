using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Picks.PickImport.Dtos;
using SportsData.Api.Application.UI.Picks.PickImport.Planner;
using SportsData.Api.Extensions;
using SportsData.Api.Infrastructure.Data;
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
    private readonly AppDataContext _dataContext;
    private readonly IPickImportPlanner _planner;
    private readonly IDateTimeProvider _dateTime;

    public GetPickImportPreviewQueryHandler(
        AppDataContext dataContext,
        IPickImportPlanner planner,
        IDateTimeProvider dateTime)
    {
        _dataContext = dataContext;
        _planner = planner;
        _dateTime = dateTime;
    }

    public async Task<Result<PickImportPreviewDto>> ExecuteAsync(
        GetPickImportPreviewQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.SourceLeagueId == query.TargetLeagueId)
        {
            return new Failure<PickImportPreviewDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(query.SourceLeagueId), "Source and target leagues must be different.")]);
        }

        // Membership-gate both leagues in one round-trip; also carries the group
        // fields we need (pick type / confidence flag). Deactivated leagues are
        // excluded so a wound-down league can't be a source or target.
        var memberships = await _dataContext.PickemGroupMembers
            .AsNoTracking()
            .Where(m => m.UserId == query.UserId
                        && m.Group.DeactivatedUtc == null
                        && (m.PickemGroupId == query.SourceLeagueId || m.PickemGroupId == query.TargetLeagueId))
            .Select(m => new
            {
                m.PickemGroupId,
                m.Group.PickType,
                m.Group.UseConfidencePoints
            })
            .ToListAsync(cancellationToken);

        var target = memberships.FirstOrDefault(m => m.PickemGroupId == query.TargetLeagueId);
        if (target is null)
        {
            return new Failure<PickImportPreviewDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.TargetLeagueId), "Target league not found or you are not a member.")]);
        }

        var source = memberships.FirstOrDefault(m => m.PickemGroupId == query.SourceLeagueId);
        if (source is null)
        {
            return new Failure<PickImportPreviewDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.SourceLeagueId), "Source league not found or you are not a member.")]);
        }

        if (source.PickType != target.PickType)
        {
            return new Failure<PickImportPreviewDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(query.SourceLeagueId), "Source and target leagues must be the same pick type.")]);
        }

        var nowUtc = _dateTime.UtcNow();

        // The target league's matchups define the universe.
        var targetMatchups = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.GroupId == query.TargetLeagueId)
            .ToListAsync(cancellationToken);

        var contestIds = targetMatchups.Select(m => m.ContestId).ToList();

        // The user's source-league selections for those shared contests (team picks only).
        var sourceSelections = await _dataContext.UserPicks
            .AsNoTracking()
            .Where(p => p.UserId == query.UserId
                        && p.PickemGroupId == query.SourceLeagueId
                        && p.FranchiseSeasonId != null
                        && contestIds.Contains(p.ContestId))
            .Select(p => new { p.ContestId, FranchiseSeasonId = p.FranchiseSeasonId!.Value, PickId = p.Id })
            .ToListAsync(cancellationToken);

        // The user's existing target-league selections for those contests (team picks only).
        var existingTargetSelections = await _dataContext.UserPicks
            .AsNoTracking()
            .Where(p => p.UserId == query.UserId
                        && p.PickemGroupId == query.TargetLeagueId
                        && p.FranchiseSeasonId != null
                        && contestIds.Contains(p.ContestId))
            .Select(p => new { p.ContestId, FranchiseSeasonId = p.FranchiseSeasonId!.Value })
            .ToListAsync(cancellationToken);

        var input = new PickImportPlanInput(
            targetMatchups
                .Select(m => new PickImportTargetMatchup(
                    m.ContestId,
                    m.SeasonWeek,
                    m.IsLocked(nowUtc),
                    m.Headline,
                    m.HomeSpread))
                .ToList(),
            sourceSelections
                .GroupBy(s => s.ContestId)
                .ToDictionary(g => g.Key, g => new PickImportSourceSelection(g.First().FranchiseSeasonId, g.First().PickId)),
            existingTargetSelections
                .GroupBy(s => s.ContestId)
                .ToDictionary(g => g.Key, g => g.First().FranchiseSeasonId));

        var plan = _planner.BuildPlan(input);

        var dto = new PickImportPreviewDto
        {
            SourceLeagueId = query.SourceLeagueId,
            TargetLeagueId = query.TargetLeagueId,
            TargetUsesConfidencePoints = target.UseConfidencePoints,
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
