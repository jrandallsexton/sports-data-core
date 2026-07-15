using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Extensions;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Picks.PickImport.Planner;

/// <summary>
/// Loads and classifies a cross-league pick import for a user: validates
/// membership / pick-type, reads the target's matchups plus the user's source
/// and existing-target selections, and runs <see cref="IPickImportPlanner"/>.
/// Both the preview (dry-run) and the commit path go through here so they can
/// never classify the same request differently.
/// See docs/features/pick-import-across-leagues.md.
/// </summary>
public interface IPickImportPlanService
{
    Task<Result<PickImportPlanContext>> BuildAsync(
        Guid userId,
        Guid sourceLeagueId,
        Guid targetLeagueId,
        CancellationToken cancellationToken = default);
}

public sealed record PickImportPlanContext(
    Guid SourceLeagueId,
    Guid TargetLeagueId,
    PickType TargetPickType,
    bool TargetUsesConfidencePoints,
    PickImportPlan Plan);

public class PickImportPlanService : IPickImportPlanService
{
    private readonly AppDataContext _dataContext;
    private readonly IPickImportPlanner _planner;
    private readonly IDateTimeProvider _dateTime;

    public PickImportPlanService(
        AppDataContext dataContext,
        IPickImportPlanner planner,
        IDateTimeProvider dateTime)
    {
        _dataContext = dataContext;
        _planner = planner;
        _dateTime = dateTime;
    }

    public async Task<Result<PickImportPlanContext>> BuildAsync(
        Guid userId,
        Guid sourceLeagueId,
        Guid targetLeagueId,
        CancellationToken cancellationToken = default)
    {
        if (sourceLeagueId == targetLeagueId)
        {
            return Fail(ResultStatus.Validation, nameof(sourceLeagueId), "Source and target leagues must be different.");
        }

        // Membership-gate both leagues in one round-trip; also carries the group
        // fields we need (pick type / confidence flag). Deactivated leagues are
        // excluded so a wound-down league can't be a source or target.
        var memberships = await _dataContext.PickemGroupMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId
                        && m.Group.DeactivatedUtc == null
                        && (m.PickemGroupId == sourceLeagueId || m.PickemGroupId == targetLeagueId))
            .Select(m => new
            {
                m.PickemGroupId,
                m.Group.PickType,
                m.Group.UseConfidencePoints
            })
            .ToListAsync(cancellationToken);

        var target = memberships.FirstOrDefault(m => m.PickemGroupId == targetLeagueId);
        if (target is null)
        {
            return Fail(ResultStatus.NotFound, nameof(targetLeagueId), "Target league not found or you are not a member.");
        }

        var source = memberships.FirstOrDefault(m => m.PickemGroupId == sourceLeagueId);
        if (source is null)
        {
            return Fail(ResultStatus.NotFound, nameof(sourceLeagueId), "Source league not found or you are not a member.");
        }

        if (source.PickType != target.PickType)
        {
            return Fail(ResultStatus.Validation, nameof(sourceLeagueId), "Source and target leagues must be the same pick type.");
        }

        var nowUtc = _dateTime.UtcNow();

        // The target league's matchups define the universe. Project only the
        // fields the plan needs (lock state derives from StartDateUtc) rather
        // than materializing the full ~30-column entity.
        var targetMatchups = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.GroupId == targetLeagueId)
            .Select(m => new
            {
                m.ContestId,
                m.SeasonWeek,
                m.StartDateUtc,
                m.Headline,
                m.HomeSpread
            })
            .ToListAsync(cancellationToken);

        var contestIds = targetMatchups.Select(m => m.ContestId).ToList();

        // The user's source-league selections for those shared contests (team picks only).
        var sourceSelections = await _dataContext.UserPicks
            .AsNoTracking()
            .Where(p => p.UserId == userId
                        && p.PickemGroupId == sourceLeagueId
                        && p.FranchiseSeasonId != null
                        && contestIds.Contains(p.ContestId))
            .Select(p => new { p.ContestId, FranchiseSeasonId = p.FranchiseSeasonId!.Value, PickId = p.Id })
            .ToListAsync(cancellationToken);

        // The user's existing target-league selections for those contests (team picks only).
        var existingTargetSelections = await _dataContext.UserPicks
            .AsNoTracking()
            .Where(p => p.UserId == userId
                        && p.PickemGroupId == targetLeagueId
                        && p.FranchiseSeasonId != null
                        && contestIds.Contains(p.ContestId))
            .Select(p => new { p.ContestId, FranchiseSeasonId = p.FranchiseSeasonId!.Value })
            .ToListAsync(cancellationToken);

        var input = new PickImportPlanInput(
            targetMatchups
                .Select(m => new PickImportTargetMatchup(
                    m.ContestId,
                    m.SeasonWeek,
                    PickemGroupMatchupExtensions.IsStartLocked(m.StartDateUtc, nowUtc),
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

        return new Success<PickImportPlanContext>(
            new PickImportPlanContext(sourceLeagueId, targetLeagueId, target.PickType, target.UseConfidencePoints, plan));
    }

    private static Failure<PickImportPlanContext> Fail(ResultStatus status, string field, string message) =>
        new(default!, status, [new ValidationFailure(field, message)]);
}
