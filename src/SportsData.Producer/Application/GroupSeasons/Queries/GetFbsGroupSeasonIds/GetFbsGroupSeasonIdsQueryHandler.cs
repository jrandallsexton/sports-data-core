using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.GroupSeasons.Queries.GetFbsGroupSeasonIds;

public interface IGetFbsGroupSeasonIdsQueryHandler
{
    Task<Result<HashSet<Guid>>> ExecuteAsync(
        GetFbsGroupSeasonIdsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetFbsGroupSeasonIdsQueryHandler : IGetFbsGroupSeasonIdsQueryHandler
{
    private readonly TeamSportDataContext _dataContext;

    public GetFbsGroupSeasonIdsQueryHandler(TeamSportDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<HashSet<Guid>>> ExecuteAsync(
        GetFbsGroupSeasonIdsQuery query,
        CancellationToken cancellationToken = default)
    {
        var groupSeasons = await _dataContext.GroupSeasons
            .AsNoTracking()
            .Where(gs => gs.SeasonYear == query.SeasonYear)
            .ToListAsync(cancellationToken);

        // Get all FBS roots (may be duplicates due to ESPN data).
        // Slug conventions differ by hierarchy vintage: 2025+ uses
        // "fbs-i-a"; the backfilled pre-2025 hierarchies use "fbs"
        // (whose descendants include "fbs-indep", verified in prod).
        // The multi-root descendant union below dedupes any overlap.
        var fbsRoots = groupSeasons
            .Where(gs => gs.Slug is "fbs-i-a" or "fbs")
            .ToList();

        if (fbsRoots.Count == 0)
        {
            return new Failure<HashSet<Guid>>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.SeasonYear), $"FBS group root(s) not found for season {query.SeasonYear}.")]);
        }

        // Collect all descendant group IDs from all FBS roots
        var fbsGroupIds = new HashSet<Guid>();
        foreach (var root in fbsRoots)
        {
            var descendants = GetAllDescendantGroupIds(root.Id, groupSeasons);
            foreach (var id in descendants)
                fbsGroupIds.Add(id);
        }

        return new Success<HashSet<Guid>>(fbsGroupIds);
    }

    private static HashSet<Guid> GetAllDescendantGroupIds(Guid rootId, List<GroupSeason> allGroups)
    {
        var result = new HashSet<Guid> { rootId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var children = allGroups
                .Where(g => g.ParentId == currentId)
                .Select(g => g.Id);

            foreach (var childId in children)
            {
                if (result.Add(childId))
                    queue.Enqueue(childId);
            }
        }

        return result;
    }
}
