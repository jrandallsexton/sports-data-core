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
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<GetMeQueryHandler> _logger;

    public GetMeQueryHandler(
        IOptions<ApiConfig> config,
        AppDataContext db,
        IDateTimeProvider dateTimeProvider,
        ILogger<GetMeQueryHandler> logger)
    {
        _config = config.Value;
        _db = db;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<UserDto>> ExecuteAsync(
        GetMeQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting user details for UserId={UserId}", query.UserId);

        // Captured once so the "current week" projection evaluates against a single
        // point in time per request — and so IDateTimeProvider (not raw UtcNow) drives
        // it, keeping the projection deterministic under unit-test mocks.
        var nowUtc = _dateTimeProvider.UtcNow();

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
                Username = user.Username,
                Timezone = user.Timezone,
                LastLoginUtc = user.LastLoginUtc,
                IsAdmin = user.IsAdmin || user.Id == _config.UserIdSystem,
                IsReadOnly = user.IsReadOnly,
                Leagues = user.GroupMemberships
                    // Hide deactivated leagues (season-long leagues that ended,
                    // short-lived leagues past their window, commissioner-closed
                    // leagues). PickemGroup.DeactivatedUtc is stamped manually
                    // today; a background job will automate it later. A future
                    // "Past Seasons" endpoint can surface the excluded rows.
                    .Where(m => m.Group.DeactivatedUtc == null)
                    // Newest-first so a just-created league lands at the top of
                    // YourLeaguesCard on the next /me fetch — matches the
                    // commissioner's mental model coming out of the create flow.
                    .OrderByDescending(m => m.Group.CreatedUtc)
                    .Select(m => new UserDto.UserLeagueMembership
                    {
                        Id = m.Group.Id,
                        Name = m.Group.Name,
                        Sport = m.Group.Sport,
                        // Dedupe: some leagues have multiple PickemGroupWeek rows with
                        // the same SeasonWeek number (e.g. a preseason Week 1 alongside a
                        // regular-season Week 1, or rows carried over across SeasonYears).
                        // UI wants the unique set of week numbers, ascending.
                        SeasonWeeks = m.Group.Weeks
                            .Select(w => w.SeasonWeek)
                            .Distinct()
                            .OrderBy(w => w)
                            .ToList(),
                        // "Current week" = smallest SeasonWeek that still has an unstarted
                        // matchup. Picks-page UX intent: when the user opens the page mid-
                        // season, drop them on the week they should be picking, not the
                        // last week of the season. Coalesces to MAX(SeasonWeek) once every
                        // matchup has kicked off so a season-over visit lands on the most
                        // recent week instead of nothing.
                        CurrentSeasonWeek = m.Group.Weeks
                            .Where(w => w.Matchups.Any(mm => mm.StartDateUtc > nowUtc))
                            .OrderBy(w => w.SeasonWeek)
                            .Select(w => (int?)w.SeasonWeek)
                            .FirstOrDefault()
                            ?? m.Group.Weeks
                                .OrderByDescending(w => w.SeasonWeek)
                                .Select(w => (int?)w.SeasonWeek)
                                .FirstOrDefault()
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

        _logger.LogDebug("User retrieved successfully. UserId={UserId}", query.UserId);

        return new Success<UserDto>(userDto);
    }
}
