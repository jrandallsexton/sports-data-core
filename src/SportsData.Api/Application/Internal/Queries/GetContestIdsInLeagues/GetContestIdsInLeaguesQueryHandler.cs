using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Internal.Queries.GetContestIdsInLeagues;

public record GetContestIdsInLeaguesQuery(Guid[] ContestIds);

public interface IGetContestIdsInLeaguesQueryHandler
{
    Task<Result<List<Guid>>> ExecuteAsync(
        GetContestIdsInLeaguesQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Which of the supplied contests appear in any league's matchups (team
/// or player pick'em — both generate PickemGroupMatchup rows), any season
/// week. Powers Producer's stream-scheduling filter so live-sourcing
/// covers only games that back a league.
/// </summary>
public class GetContestIdsInLeaguesQueryHandler : IGetContestIdsInLeaguesQueryHandler
{
    private readonly AppDataContext _dataContext;

    public GetContestIdsInLeaguesQueryHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<List<Guid>>> ExecuteAsync(
        GetContestIdsInLeaguesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.ContestIds.Length == 0)
        {
            return new Success<List<Guid>>([]);
        }

        var inUse = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => query.ContestIds.Contains(m.ContestId))
            .Select(m => m.ContestId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new Success<List<Guid>>(inUse);
    }
}
