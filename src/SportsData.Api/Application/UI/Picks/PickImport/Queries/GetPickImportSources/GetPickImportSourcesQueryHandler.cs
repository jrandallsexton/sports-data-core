using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.PickImport.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.PickImport.Queries.GetPickImportSources;

public interface IGetPickImportSourcesQueryHandler
{
    Task<Result<List<PickImportSourceDto>>> ExecuteAsync(
        GetPickImportSourcesQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lists the user's other active same-type leagues that share at least one contest
/// with the target — the candidates for the import source picker.
/// See docs/features/pick-import-across-leagues.md.
/// </summary>
public class GetPickImportSourcesQueryHandler : IGetPickImportSourcesQueryHandler
{
    private readonly AppDataContext _dataContext;

    public GetPickImportSourcesQueryHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<List<PickImportSourceDto>>> ExecuteAsync(
        GetPickImportSourcesQuery query,
        CancellationToken cancellationToken = default)
    {
        // The target must exist and the caller must be a member of it.
        var target = await _dataContext.PickemGroupMembers
            .AsNoTracking()
            .Where(m => m.UserId == query.UserId
                        && m.PickemGroupId == query.TargetLeagueId
                        && m.Group.DeactivatedUtc == null)
            .Select(m => new { m.Group.PickType })
            .FirstOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            return new Failure<List<PickImportSourceDto>>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.TargetLeagueId), "Target league not found or you are not a member.")]);
        }

        // Candidate sources: the user's other active leagues of the same pick type.
        var candidates = await _dataContext.PickemGroupMembers
            .AsNoTracking()
            .Where(m => m.UserId == query.UserId
                        && m.PickemGroupId != query.TargetLeagueId
                        && m.Group.DeactivatedUtc == null
                        && m.Group.PickType == target.PickType)
            .Select(m => new
            {
                m.PickemGroupId,
                m.Group.Name,
                m.Group.Sport,
                m.Group.PickType,
                m.Group.UseConfidencePoints,
                MemberCount = m.Group.Members.Count
            })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return new Success<List<PickImportSourceDto>>([]);
        }

        var candidateIds = candidates.Select(c => c.PickemGroupId).ToList();

        var targetContestIds = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.GroupId == query.TargetLeagueId)
            .Select(m => m.ContestId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Count shared contests per candidate against the target's contest set.
        var sharedCounts = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => candidateIds.Contains(m.GroupId) && targetContestIds.Contains(m.ContestId))
            .GroupBy(m => m.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Select(m => m.ContestId).Distinct().Count() })
            .ToListAsync(cancellationToken);

        var sharedByGroup = sharedCounts.ToDictionary(x => x.GroupId, x => x.Count);

        var sources = candidates
            .Where(c => sharedByGroup.ContainsKey(c.PickemGroupId))
            .Select(c => new PickImportSourceDto
            {
                LeagueId = c.PickemGroupId,
                Name = c.Name,
                Sport = c.Sport.ToString(),
                PickType = c.PickType.ToString(),
                UseConfidencePoints = c.UseConfidencePoints,
                SharedContestCount = sharedByGroup[c.PickemGroupId],
                MemberCount = c.MemberCount
            })
            .OrderByDescending(s => s.SharedContestCount)
            .ThenBy(s => s.Name)
            .ToList();

        return new Success<List<PickImportSourceDto>>(sources);
    }
}
