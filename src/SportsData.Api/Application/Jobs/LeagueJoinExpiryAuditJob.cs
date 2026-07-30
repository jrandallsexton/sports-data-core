using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.PickemGroups;
using SportsData.Api.Infrastructure.Data;

namespace SportsData.Api.Application.Jobs;

/// <summary>
/// Self-healing sweep for <c>PickemGroup.InvitationsExpireUtc</c>: recomputes
/// the join-expiry for every active league. Doubles as the BACKFILL for
/// leagues created before the field existed (audit-job idiom — pointed at the
/// existing data, the sweep IS the migration path; no throwaway script).
///
/// Hourly rather than daily: during season, drop-week expiries refine from
/// calendar-provisional to first-kickoff-precise as weekly slates land, and
/// the cost is a handful of indexed queries per active league.
/// </summary>
public class LeagueJoinExpiryAuditJob
{
    private readonly ILogger<LeagueJoinExpiryAuditJob> _logger;
    private readonly AppDataContext _dataContext;
    private readonly ILeagueJoinExpiryCalculator _calculator;

    public LeagueJoinExpiryAuditJob(
        ILogger<LeagueJoinExpiryAuditJob> logger,
        AppDataContext dataContext,
        ILeagueJoinExpiryCalculator calculator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _calculator = calculator;
    }

    public async Task ExecuteAsync()
    {
        var activeLeagueIds = await _dataContext.PickemGroups
            .AsNoTracking()
            .Where(g => g.DeactivatedUtc == null)
            .Select(g => g.Id)
            .ToListAsync();

        _logger.LogInformation(
            "Join-expiry sweep over {Count} active league(s).", activeLeagueIds.Count);

        foreach (var id in activeLeagueIds)
        {
            // Per-league isolation: one league's failure (e.g. season overview
            // unavailable for its sport) must not starve the rest.
            try
            {
                await _calculator.RecomputeAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Join-expiry recompute failed for league {GroupId}.", id);
            }
        }
    }
}
