using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Messageboard.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.Messageboard.Queries.GetThreads;

public interface IGetThreadsQueryHandler
{
    Task<PageResult<MessageThread>> ExecuteAsync(
        GetThreadsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetThreadsQueryHandler : IGetThreadsQueryHandler
{
    private readonly AppDataContext _dataContext;

    public GetThreadsQueryHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<PageResult<MessageThread>> ExecuteAsync(
        GetThreadsQuery query,
        CancellationToken cancellationToken = default)
    {
        long? cursorTicks = long.TryParse(query.Cursor, out var t) ? t : null;

        var q = _dataContext.Set<MessageThread>()
            .AsNoTracking()
            .Include(t => t.User)
            .Where(t0 => t0.GroupId == query.GroupId);

        if (cursorTicks is not null)
        {
            q = q.Where(t0 => t0.LastActivityAt.Ticks < cursorTicks.Value);
        }

        q = q.OrderByDescending(t0 => t0.LastActivityAt);

        var items = await q.Take(query.Limit + 1).ToListAsync(cancellationToken);
        string? next = null;

        if (items.Count > query.Limit)
        {
            var last = items[^2];
            next = last.LastActivityAt.Ticks.ToString();
            items.RemoveAt(items.Count - 1);
        }

        return new PageResult<MessageThread>(items, next);
    }
}
