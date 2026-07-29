using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.Leagues.Authorization;

public interface ILeagueMembershipGuard
{
    /// <summary>
    /// True when <paramref name="userId"/> belongs to <paramref name="leagueId"/>.
    /// </summary>
    Task<bool> IsMemberAsync(Guid leagueId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when <paramref name="userId"/> belongs to the league that owns
    /// <paramref name="threadId"/>. For message-board handlers, which are keyed
    /// by thread rather than by league. Returns false for an unknown thread.
    /// </summary>
    Task<bool> IsMemberOfThreadGroupAsync(Guid threadId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when <paramref name="userId"/> belongs to the league that owns the
    /// thread containing <paramref name="postId"/>. For reaction endpoints,
    /// which are keyed by post. Returns false for an unknown post.
    /// </summary>
    Task<bool> IsMemberOfPostGroupAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Single authority for "does this caller belong to this league?".
///
/// Every by-group read and write routes through this instead of trusting
/// possession of a league GUID — league ids travel in invite links, share
/// sheets, screenshots, and logs, so they are identifiers, not secrets.
/// See docs/audit/league-authorization-idor.md.
///
/// The check is an index seek: PickemGroupMember has a unique index on
/// (PickemGroupId, UserId), so this is one indexed existence query per
/// guarded request.
/// </summary>
public class LeagueMembershipGuard : ILeagueMembershipGuard
{
    private readonly AppDataContext _dbContext;

    public LeagueMembershipGuard(AppDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> IsMemberAsync(
        Guid leagueId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (leagueId == Guid.Empty || userId == Guid.Empty)
            return Task.FromResult(false);

        return _dbContext.PickemGroupMembers
            .AsNoTracking()
            .AnyAsync(m => m.PickemGroupId == leagueId && m.UserId == userId, cancellationToken);
    }

    public async Task<bool> IsMemberOfThreadGroupAsync(
        Guid threadId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (threadId == Guid.Empty || userId == Guid.Empty)
            return false;

        // Single join rather than two round-trips: resolve the thread's league
        // and test membership in one query. An unknown thread yields no rows,
        // so a bogus threadId is denied rather than erroring.
        return await _dbContext.Set<MessageThread>()
            .AsNoTracking()
            .Where(t => t.Id == threadId)
            .AnyAsync(
                t => _dbContext.PickemGroupMembers
                    .Any(m => m.PickemGroupId == t.GroupId && m.UserId == userId),
                cancellationToken);
    }

    public async Task<bool> IsMemberOfPostGroupAsync(
        Guid postId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (postId == Guid.Empty || userId == Guid.Empty)
            return false;

        return await _dbContext.Set<MessagePost>()
            .AsNoTracking()
            .Where(p => p.Id == postId)
            .AnyAsync(
                p => _dbContext.Set<MessageThread>()
                    .Any(t => t.Id == p.ThreadId
                        && _dbContext.PickemGroupMembers
                            .Any(m => m.PickemGroupId == t.GroupId && m.UserId == userId)),
                cancellationToken);
    }
}
