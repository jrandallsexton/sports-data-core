using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;

public interface IGetUserPicksByGroupAndWeekQueryHandler
{
    Task<Result<UserPicksResultDto>> ExecuteAsync(
        GetUserPicksByGroupAndWeekQuery query,
        CancellationToken cancellationToken = default);
}

public class GetUserPicksByGroupAndWeekQueryHandler : IGetUserPicksByGroupAndWeekQueryHandler
{
    private readonly ILogger<GetUserPicksByGroupAndWeekQueryHandler> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetUserPicksByGroupAndWeekQueryHandler(
        ILogger<GetUserPicksByGroupAndWeekQueryHandler> logger,
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<UserPicksResultDto>> ExecuteAsync(
        GetUserPicksByGroupAndWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var picks = await _dataContext.UserPicks
            .AsNoTracking()
            .Where(p =>
                p.PickemGroupId == query.GroupId &&
                p.UserId == query.UserId &&
                p.Week == query.WeekNumber)
            .Select(p => new UserPickDto
            {
                Id = p.Id,
                UserId = p.UserId,
                User = p.User.DisplayName,
                ConfidencePoints = p.ConfidencePoints,
                ContestId = p.ContestId,
                FranchiseSeasonId = p.FranchiseSeasonId ?? Guid.Empty,
                IsCorrect = p.IsCorrect,
                PickType = p.PickType,
                TiebreakerGuessTotal = p.TiebreakerGuessTotal,
                PointsAwarded = p.PointsAwarded,
                IsSynthetic = p.User.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        // The group-week's matchups (picked or not) — covered by the
        // (GroupId, SeasonYear, SeasonWeek) index on PickemGroupMatchup. A
        // week holds at most a few dozen rows, so the projection is cheap and
        // lets pending be computed without a Contest-service round-trip.
        var matchups = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m =>
                m.GroupId == query.GroupId &&
                m.SeasonWeek == query.WeekNumber)
            .Select(m => new { m.ContestId, m.StartDateUtc })
            .ToListAsync(cancellationToken);

        // A matchup is PENDING for this user when their week-outcome can still
        // change or is still being resolved:
        //   - unpicked and the game hasn't started (still actionable), or
        //   - picked but not yet scored (IsCorrect == null: game in progress
        //     or awaiting the scoring processor) — bounded by a window after
        //     kickoff.
        // Unpicked-and-started games are NOT pending — they can never be
        // picked, so they're already a decided no-result (the X bucket).
        // PendingCount == 0 therefore means the user's results glance is
        // final, even if a game they skipped is still in progress.
        //
        // The window exists because canceled/void contests never get scored:
        // PickScoringProcessor skips result-less and unfinalized matchups, so
        // their picks keep IsCorrect == null forever. Unbounded, one canceled
        // game would hold PendingCount above zero until league deactivation.
        // 24h generously covers the longest games plus scoring/backfill lag;
        // beyond it a never-scored pick folds into the X bucket — the same
        // semantics deactivated leagues already display.
        var now = _dateTimeProvider.UtcNow();
        var scoringHorizon = now - TimeSpan.FromHours(24);
        var picksByContest = picks.ToDictionary(p => p.ContestId);
        var pendingCount = matchups.Count(m =>
            picksByContest.TryGetValue(m.ContestId, out var pick)
                ? pick.IsCorrect is null && m.StartDateUtc > scoringHorizon
                : m.StartDateUtc > now);

        return new Success<UserPicksResultDto>(new UserPicksResultDto
        {
            Picks = picks,
            TotalMatchups = matchups.Count,
            CorrectCount = picks.Count(p => p.IsCorrect == true),
            IncorrectCount = picks.Count(p => p.IsCorrect == false),
            PendingCount = pendingCount
        });
    }
}
