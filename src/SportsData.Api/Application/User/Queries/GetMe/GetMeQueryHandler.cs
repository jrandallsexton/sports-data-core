using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SportsData.Api.Application.User.Dtos;
using SportsData.Api.Config;
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
    private readonly ApiConfig _config;
    private readonly AppDataContext _db;
    private readonly ILogger<GetMeQueryHandler> _logger;

    public GetMeQueryHandler(
        IOptions<ApiConfig> config,
        AppDataContext db,
        ILogger<GetMeQueryHandler> logger)
    {
        _config = config.Value;
        _db = db;
        _logger = logger;
    }

    public async Task<Result<UserDto>> ExecuteAsync(
        GetMeQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting user details for UserId={UserId}", query.UserId);

        var userDto = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == query.UserId)
            .Select(user => new UserDto
            {
                Id = user.Id,
                FirebaseUid = user.FirebaseUid,
                Email = user.Email,
                DisplayName = user.DisplayName,
                LastLoginUtc = user.LastLoginUtc,
                IsAdmin = user.IsAdmin || user.Id == _config.UserIdSystem,
                IsReadOnly = user.IsReadOnly,
                Leagues = user.GroupMemberships
                    .Select(m => new UserDto.UserLeagueMembership
                    {
                        Id = m.Group.Id,
                        Name = m.Group.Name,
                        MaxSeasonWeek = m.Group.Weeks
                            .Max(w => (int?)w.SeasonWeek)
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (userDto is null)
        {
            _logger.LogWarning("User not found. UserId={UserId}", query.UserId);
            return new Failure<UserDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure("UserId", $"User with ID {query.UserId} not found.")]);
        }

        _logger.LogInformation("User retrieved successfully. UserId={UserId}", query.UserId);

        return new Success<UserDto>(userDto);
    }
}
