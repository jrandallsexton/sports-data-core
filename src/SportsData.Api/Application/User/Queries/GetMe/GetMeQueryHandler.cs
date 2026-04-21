using System.Diagnostics;

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
            .AsSplitQuery()
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
                        // Dedupe: some leagues have multiple PickemGroupWeek rows with
                        // the same SeasonWeek number (e.g. a preseason Week 1 alongside a
                        // regular-season Week 1, or rows carried over across SeasonYears).
                        // UI wants the unique set of week numbers, ascending.
                        SeasonWeeks = m.Group.Weeks
                            .Select(w => w.SeasonWeek)
                            .Distinct()
                            .OrderBy(w => w)
                            .ToList()
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

        // Defensive invariant check. SeasonWeeks is contractually ascending + distinct
        // (see UserDto.UserLeagueMembership.SeasonWeeks <remarks>). The EF projection
        // above enforces it via .Distinct().OrderBy(); this assert guards against a
        // future change to that projection silently breaking the contract.
        foreach (var league in userDto.Leagues)
        {
            Debug.Assert(
                league.SeasonWeeks.SequenceEqual(league.SeasonWeeks.Distinct().OrderBy(w => w)),
                $"SeasonWeeks for league {league.Id} is not ascending+distinct.");
        }

        _logger.LogInformation("User retrieved successfully. UserId={UserId}", query.UserId);

        return new Success<UserDto>(userDto);
    }
}
