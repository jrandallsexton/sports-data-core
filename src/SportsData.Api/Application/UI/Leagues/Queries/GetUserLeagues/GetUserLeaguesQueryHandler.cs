using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetUserLeagues;

public interface IGetUserLeaguesQueryHandler
{
    Task<Result<List<LeagueSummaryDto>>> ExecuteAsync(GetUserLeaguesQuery query, CancellationToken cancellationToken = default);
}

public class GetUserLeaguesQueryHandler : IGetUserLeaguesQueryHandler
{
    private readonly AppDataContext _dbContext;

    public GetUserLeaguesQueryHandler(AppDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<LeagueSummaryDto>>> ExecuteAsync(
        GetUserLeaguesQuery query,
        CancellationToken cancellationToken = default)
    {
        var leagues = await _dbContext.PickemGroupMembers
            .AsNoTracking()
            .Where(m => m.UserId == query.UserId)
            // Hide deactivated leagues — matches the filter on /user/me. A future
            // "Past Seasons" endpoint will surface the excluded rows explicitly.
            .Where(m => m.Group.DeactivatedUtc == null)
            .Include(m => m.Group)
            .Select(m => new LeagueSummaryDto
            {
                Id = m.Group.Id,
                Name = m.Group.Name,
                Sport = m.Group.Sport.ToString(),
                LeagueType = m.Group.PickType.ToString(),
                UseConfidencePoints = m.Group.UseConfidencePoints,
                MemberCount = m.Group.Members.Count
            })
            .ToListAsync(cancellationToken);

        return new Success<List<LeagueSummaryDto>>(leagues);
    }
}
