using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;

public interface IGetUserPicksByGroupAndWeekQueryHandler
{
    Task<Result<List<UserPickDto>>> ExecuteAsync(
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

    public async Task<Result<List<UserPickDto>>> ExecuteAsync(
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
                FranchiseId = p.FranchiseId ?? Guid.Empty,
                IsCorrect = p.IsCorrect,
                PickType = p.PickType,
                TiebreakerGuessTotal = p.TiebreakerGuessTotal,
                PointsAwarded = p.PointsAwarded,
                IsSynthetic = p.User.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        return new Success<List<UserPickDto>>(picks);
    }
}
