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
            // Hide deactivated leagues unless the caller opts in — matches the
            // filter on /user/me. Opting in is how the My Leagues page powers its
            // "show past leagues" toggle; the rows carry DeactivatedUtc so the UI
            // can mark them read-only.
            .Where(m => query.IncludeDeactivated || m.Group.DeactivatedUtc == null)
            .Include(m => m.Group)
            .Select(m => new LeagueSummaryDto
            {
                Id = m.Group.Id,
                Name = m.Group.Name,
                Description = m.Group.Description,
                Sport = m.Group.Sport.ToString(),
                League = m.Group.League.ToString(),
                LeagueType = m.Group.PickType.ToString(),
                UseConfidencePoints = m.Group.UseConfidencePoints,
                MemberCount = m.Group.Members.Count,
                SeasonYear = m.Group.SeasonYear,
                // Distinct week numbers, ascending. Some leagues have multiple
                // PickemGroupWeek rows with the same SeasonWeek (e.g. preseason +
                // regular-season Week 1); the UI wants the unique set. Mirrors the
                // projection on /user/me.
                SeasonWeeks = m.Group.Weeks
                    .Select(w => w.SeasonWeek)
                    .Distinct()
                    .OrderBy(w => w)
                    .ToList(),
                DeactivatedUtc = m.Group.DeactivatedUtc,
                CreatedUtc = m.Group.CreatedUtc
            })
            .ToListAsync(cancellationToken);

        return new Success<List<LeagueSummaryDto>>(leagues);
    }
}
