using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Competition;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutCompetitors;

public interface IGetCompetitionsWithoutCompetitorsQueryHandler
{
    Task<Result<List<CompetitionWithoutCompetitorsDto>>> ExecuteAsync(
        GetCompetitionsWithoutCompetitorsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Moved from API's CanonicalAdminDataProvider (embedded SQL against one
/// canonical database) so each Producer answers for its own sport. Same
/// rule: every competition whose competitor count is not exactly 2, fewest
/// first. Unlike the SQL it replaces, it returns the contest's StartDateUtc
/// (the old query never selected it, so the field arrived as 0001-01-01).
/// </summary>
public class GetCompetitionsWithoutCompetitorsQueryHandler : IGetCompetitionsWithoutCompetitorsQueryHandler
{
    private readonly TeamSportDataContext _dataContext;

    public GetCompetitionsWithoutCompetitorsQueryHandler(TeamSportDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<List<CompetitionWithoutCompetitorsDto>>> ExecuteAsync(
        GetCompetitionsWithoutCompetitorsQuery query,
        CancellationToken cancellationToken = default)
    {
        var competitions = await _dataContext.Competitions
            .AsNoTracking()
            .Select(c => new CompetitionWithoutCompetitorsDto
            {
                Sport = c.Contest.Sport,
                CompetitionId = c.Id,
                ContestId = c.ContestId,
                StartDateUtc = c.Contest.StartDateUtc,
                CompetitionName = c.Contest.Name,
                CompetitorCount = c.Competitors.Count
            })
            .Where(x => x.CompetitorCount != 2)
            .OrderBy(x => x.CompetitorCount)
            .ThenBy(x => x.StartDateUtc)
            .ToListAsync(cancellationToken);

        return new Success<List<CompetitionWithoutCompetitorsDto>>(competitions);
    }
}
