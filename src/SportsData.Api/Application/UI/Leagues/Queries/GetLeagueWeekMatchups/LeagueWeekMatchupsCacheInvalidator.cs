using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;

/// <summary>
/// Evicts every cached league-week payload a contest appears in. The payload
/// carries per-matchup preview state (IsPreviewAvailable, IsPreviewReviewed),
/// so anything that changes a contest's preview - generation, approval,
/// rejection - must call this before the client is told to refetch.
/// </summary>
public interface ILeagueWeekMatchupsCacheInvalidator
{
    Task EvictForContestAsync(Guid contestId, CancellationToken cancellationToken = default);
}

public sealed class LeagueWeekMatchupsCacheInvalidator : ILeagueWeekMatchupsCacheInvalidator
{
    private readonly AppDataContext _dataContext;
    private readonly ILeagueWeekMatchupsCache _cache;
    private readonly ILogger<LeagueWeekMatchupsCacheInvalidator> _logger;

    public LeagueWeekMatchupsCacheInvalidator(
        AppDataContext dataContext,
        ILeagueWeekMatchupsCache cache,
        ILogger<LeagueWeekMatchupsCacheInvalidator> logger)
    {
        _dataContext = dataContext;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Fail-open, like the cache itself: the change that triggered this is
    /// already committed and must not be faulted by a lookup or Redis failure.
    /// Worst case the stale entry lives out its TTL.
    /// </summary>
    public async Task EvictForContestAsync(Guid contestId, CancellationToken cancellationToken = default)
    {
        try
        {
            var leagueWeeks = await _dataContext.PickemGroupMatchups
                .AsNoTracking()
                .Where(m => m.ContestId == contestId)
                .Select(m => new { m.GroupId, m.SeasonWeek })
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var lw in leagueWeeks)
            {
                await _cache.RemoveAsync(lw.GroupId, lw.SeasonWeek);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not evict league-week caches for contest {ContestId}; entries will expire on their own.",
                contestId);
        }
    }
}
