using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Messageboard.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.Messageboard.Queries.GetReplies;

public interface IGetRepliesQueryHandler
{
    Task<PageResult<MessagePost>> ExecuteAsync(
        GetRepliesQuery query,
        CancellationToken cancellationToken = default);
}

public class GetRepliesQueryHandler : IGetRepliesQueryHandler
{
    private readonly ILogger<GetRepliesQueryHandler> _logger;
    private readonly AppDataContext _dataContext;

    public GetRepliesQueryHandler(
        ILogger<GetRepliesQueryHandler> logger,
        AppDataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    public async Task<PageResult<MessagePost>> ExecuteAsync(
        GetRepliesQuery query,
        CancellationToken cancellationToken = default)
    {
        long? cursorTicks = long.TryParse(query.Cursor, out var t) ? t : null;

        var q = _dataContext.Set<MessagePost>()
            .Include(p => p.User)
            .AsNoTracking()
            .Where(p => p.ThreadId == query.ThreadId && p.ParentId == query.ParentId);

        if (cursorTicks is not null)
        {
            q = q.Where(p => p.CreatedUtc.Ticks > cursorTicks.Value);
        }

        q = q.OrderBy(p => p.CreatedUtc);

        var items = await q.Take(query.Limit + 1).ToListAsync(cancellationToken);
        string? next = null;

        if (items.Count > query.Limit)
        {
            var last = items[query.Limit - 1];
            next = last.CreatedUtc.Ticks.ToString();
            items = items.Take(query.Limit).ToList();
        }

        _logger.LogInformation("Replies returned: {Count}", items.Count);

        return new PageResult<MessagePost>(items, next);
    }
}
