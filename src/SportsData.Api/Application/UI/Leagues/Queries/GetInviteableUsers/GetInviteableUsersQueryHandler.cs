using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetInviteableUsers;

public interface IGetInviteableUsersQueryHandler
{
    Task<Result<List<InviteableUserDto>>> ExecuteAsync(
        GetInviteableUsersQuery query,
        CancellationToken cancellationToken = default);
}

public class GetInviteableUsersQueryHandler : IGetInviteableUsersQueryHandler
{
    private const int MinTermLength = 2;
    private const int MaxResults = 10;

    private readonly AppDataContext _dbContext;

    public GetInviteableUsersQueryHandler(AppDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<InviteableUserDto>>> ExecuteAsync(
        GetInviteableUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        // Authorization: only a member of the league may search for users to
        // invite. Checked before any user query so a non-member can't enumerate
        // the user base through this endpoint.
        var requesterIsMember = await _dbContext.PickemGroupMembers
            .AsNoTracking()
            .AnyAsync(m => m.PickemGroupId == query.LeagueId && m.UserId == query.RequestingUserId, cancellationToken);

        if (!requesterIsMember)
            return new Failure<List<InviteableUserDto>>(
                default!,
                ResultStatus.Forbid,
                [new ValidationFailure(nameof(query.RequestingUserId), "Only league members can search for users to invite.")]);

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
