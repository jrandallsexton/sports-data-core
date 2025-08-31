using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Application.UI.Picks.Queries;
using SportsData.Api.Infrastructure.Data;

namespace SportsData.Api.Application.UI.Picks.PicksPage
{
    public interface IGetUserPicksQueryHandler
    {
        Task<List<UserPickDto>> GetUserPicksByGroupAndWeek(
            GetUserPicksQuery query,
            CancellationToken cancellationToken);
    }

    public class GetUserPicksQueryHandler : IGetUserPicksQueryHandler
    {
        private readonly ILogger<GetUserPicksQueryHandler> _logger;
        private readonly AppDataContext _dataContext;
        
        public GetUserPicksQueryHandler(
            ILogger<GetUserPicksQueryHandler> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task<List<UserPickDto>> GetUserPicksByGroupAndWeek(
            GetUserPicksQuery query,
            CancellationToken cancellationToken)
        {
            return await _dataContext.UserPicks
                .AsNoTracking()
                .Where(p =>
                    p.PickemGroupId == query.GroupId &&
                    p.UserId == query.UserId &&
                    p.Week == query.WeekNumber)
                .Select(p => new UserPickDto
                {
                    Id = p.Id,
                    ConfidencePoints = p.ConfidencePoints,
                    ContestId = p.ContestId,
                    FranchiseId = p.FranchiseId ?? Guid.Empty,
                    IsCorrect = p.IsCorrect,
                    PickType = p.PickType,
                    TiebreakerGuessTotal = p.TiebreakerGuessTotal,
                    PointsAwarded = p.PointsAwarded
                })
                .ToListAsync(cancellationToken);
        }
    }
}
