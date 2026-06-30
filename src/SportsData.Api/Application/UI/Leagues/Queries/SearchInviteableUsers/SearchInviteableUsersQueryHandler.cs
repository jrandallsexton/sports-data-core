using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Queries.SearchInviteableUsers;

public interface ISearchInviteableUsersQueryHandler
{
    Task<Result<List<InviteableUserDto>>> ExecuteAsync(
        SearchInviteableUsersQuery query,
        CancellationToken cancellationToken = default);
}

public class SearchInviteableUsersQueryHandler : ISearchInviteableUsersQueryHandler
{
    private const int MinTermLength = 2;
    private const int MaxResults = 10;

    private readonly AppDataContext _dbContext;

    public SearchInviteableUsersQueryHandler(AppDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<InviteableUserDto>>> ExecuteAsync(
        SearchInviteableUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var term = query.Q?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length < MinTermLength)
            return new Success<List<InviteableUserDto>>([]);

        var lowered = term.ToLowerInvariant();

        // Members already in the league are not invitable.
        var memberIds = _dbContext.PickemGroupMembers
            .Where(m => m.PickemGroupId == query.LeagueId)
            .Select(m => m.UserId);

        // Username is stored lowercased; DisplayName is mixed-case so lower it
        // for the match. Synthetic personas are excluded — they're not real
        // people to invite. The searcher excludes themselves.
        var results = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id != query.RequestingUserId
                && !u.IsSynthetic
                && !memberIds.Contains(u.Id)
                && (u.Username.Contains(lowered) || u.DisplayName.ToLower().Contains(lowered)))
            .OrderBy(u => u.Username)
            .Take(MaxResults)
            .Select(u => new InviteableUserDto
            {
                UserId = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName
            })
            .ToListAsync(cancellationToken);

        return new Success<List<InviteableUserDto>>(results);
    }
}
