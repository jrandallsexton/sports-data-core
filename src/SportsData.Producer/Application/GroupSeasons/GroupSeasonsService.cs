using Microsoft.EntityFrameworkCore;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.GroupSeasons
{
    public interface IGroupSeasonsService
    {
        Task<HashSet<Guid>> GetFbsGroupSeasonIds(int seasonYear);
    }

    public class GroupSeasonsService : IGroupSeasonsService
    {
        private readonly TeamSportDataContext _dataContext;

        public GroupSeasonsService(TeamSportDataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<HashSet<Guid>> GetFbsGroupSeasonIds(int seasonYear)
        {
            var groupSeasons = await _dataContext.GroupSeasons
                .Where(gs => gs.SeasonYear == seasonYear)
                .AsNoTracking()
                .ToListAsync();

            // Get all FBS roots (may be duplicates due to ESPN data).
            // Slug conventions differ by hierarchy vintage: 2025+ uses
            // "fbs-i-a"; the backfilled pre-2025 hierarchies use "fbs"
            // (whose descendants include "fbs-indep", verified in prod).
            // The multi-root descendant union below dedupes any overlap.
            var fbsRoots = groupSeasons
                .Where(gs => gs.Slug is "fbs-i-a" or "fbs")
                .ToList();

            if (!fbsRoots.Any())
                throw new InvalidOperationException("FBS group root(s) not found.");

            // Collect all descendant group IDs from all FBS roots
            var fbsGroupIds = new HashSet<Guid>();
            foreach (var root in fbsRoots)
            {
                var descendants = GetAllDescendantGroupIds(root.Id, groupSeasons);
                foreach (var id in descendants)
                    fbsGroupIds.Add(id);
            }

            return fbsGroupIds;
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
}
