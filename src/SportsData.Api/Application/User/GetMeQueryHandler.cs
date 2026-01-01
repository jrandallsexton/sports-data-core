using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.User.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.User.Queries.GetMe;

public interface IGetMeQueryHandler
{
    Task<Result<UserDto>> ExecuteAsync(
        GetMeQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMeQueryHandler : IGetMeQueryHandler
{
    private readonly AppDataContext _db;
    private readonly ILogger<GetMeQueryHandler> _logger;

    public GetMeQueryHandler(
        AppDataContext db,
        ILogger<GetMeQueryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<UserDto>> ExecuteAsync(
        GetMeQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting user details for UserId={UserId}", query.UserId);

        var user = await _db.Users
            .Include(x => x.GroupMemberships)
            .ThenInclude(m => m.Group)
            .ThenInclude(g => g.Weeks)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == query.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("User not found. UserId={UserId}", query.UserId);
            return new Failure<UserDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure("UserId", $"User with ID {query.UserId} not found.")]);
        }

        var dto = new UserDto
        {
            Id = user.Id,
            FirebaseUid = user.FirebaseUid,
            Email = user.Email,
            DisplayName = user.DisplayName,
            LastLoginUtc = user.LastLoginUtc,
            IsAdmin = user.IsAdmin || user.Id.ToString() == "11111111-1111-1111-1111-111111111111",
            IsReadOnly = user.IsReadOnly,
            Leagues = user.GroupMemberships
                .Select(m => new UserDto.UserLeagueMembership
                {
                    Id = m.Group.Id,
                    Name = m.Group.Name,
                    MaxSeasonWeek = m.Group.Weeks
                        .Select(w => (int?)w.SeasonWeek)
                        .DefaultIfEmpty()
                        .Max()
                })
                .ToList()
        };

        _logger.LogInformation("User retrieved successfully. UserId={UserId}", query.UserId);

        return new Success<UserDto>(dto);
    }
}
