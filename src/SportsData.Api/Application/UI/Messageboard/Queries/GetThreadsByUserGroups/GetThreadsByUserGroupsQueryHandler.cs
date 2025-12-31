using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Messageboard.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.Messageboard.Queries.GetThreadsByUserGroups;

public interface IGetThreadsByUserGroupsQueryHandler
{
    Task<IDictionary<Guid, PageResult<MessageThread>>> ExecuteAsync(
        GetThreadsByUserGroupsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetThreadsByUserGroupsQueryHandler : IGetThreadsByUserGroupsQueryHandler
{
    private readonly AppDataContext _dataContext;

    public GetThreadsByUserGroupsQueryHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<IDictionary<Guid, PageResult<MessageThread>>> ExecuteAsync(
        GetThreadsByUserGroupsQuery query,
        CancellationToken cancellationToken = default)
    {
        var groupIds = await _dataContext.Set<PickemGroupMember>()
            .Where(m => m.UserId == query.UserId)
            .Select(m => m.PickemGroupId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, PageResult<MessageThread>>();

        foreach (var gid in groupIds)
        {
            var q = _dataContext.Set<MessageThread>()
                .AsNoTracking()
                .Include(t => t.User)
                .Where(t => t.GroupId == gid)
                .OrderByDescending(t => t.LastActivityAt);

            var items = await q.Take(query.PerGroupLimit + 1).ToListAsync(cancellationToken);
            string? next = null;

            if (items.Count > query.PerGroupLimit)
            {
                var last = items[query.PerGroupLimit - 1];
                next = last.LastActivityAt.Ticks.ToString();
                items = items.Take(query.PerGroupLimit).ToList();
            }

            result[gid] = new PageResult<MessageThread>(items, next);
        }

        return result;
    }
}
