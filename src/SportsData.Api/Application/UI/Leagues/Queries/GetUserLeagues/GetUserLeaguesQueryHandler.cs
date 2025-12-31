using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

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
            .Where(m => m.UserId == query.UserId)
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
