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

    public GetUserPicksByGroupAndWeekQueryHandler(
        ILogger<GetUserPicksByGroupAndWeekQueryHandler> logger,
        AppDataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
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

        // Total for the group-week (picked or not) — covered by the
        // (GroupId, SeasonYear, SeasonWeek) index on PickemGroupMatchup.
        // Correct/incorrect are counted from the already-materialized picks
        // rather than issuing further queries.
        var totalMatchups = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .CountAsync(m =>
                m.GroupId == query.GroupId &&
                m.SeasonWeek == query.WeekNumber,
                cancellationToken);

        return new Success<UserPicksResultDto>(new UserPicksResultDto
        {
            Picks = picks,
            TotalMatchups = totalMatchups,
            CorrectCount = picks.Count(p => p.IsCorrect == true),
            IncorrectCount = picks.Count(p => p.IsCorrect == false)
        });
    }
}
