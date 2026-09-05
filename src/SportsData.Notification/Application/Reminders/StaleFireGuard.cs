#nullable enable

using Microsoft.EntityFrameworkCore;

using SportsData.Notification.Infrastructure.Data;

namespace SportsData.Notification.Application.Reminders;

public interface IStaleFireGuard
{
    Task<bool> IsStaleFireAsync(
        Guid userId, string jobKind, Guid targetId, int? seasonWeek, DateTime fireTimeUtc,
        DateTime? waveAnchorUtc = null);
}

/// <summary>
/// Answers "is the currently-firing Hangfire job still the scheduler's
/// intended fire for this scope?" — false means proceed; true means the
/// <c>PendingScheduledJob</c> row is gone (cancelled) or the scheduler has
/// since rescheduled to a different fire-time. Callers should treat true as
/// a no-op: finalize their claim as <c>Suppressed_StaleFire</c> and return.
///
/// <para>
/// Lookup hits the natural-key unique index
/// (UserId, JobKind, TargetId, SeasonWeek, WaveAnchorUtc) — cheap, one
/// indexed read per dispatch. ContestStart callers leave
/// <c>waveAnchorUtc</c> null (their rows store null).
/// </para>
/// </summary>
public class StaleFireGuard : IStaleFireGuard
{
    private readonly ILogger<StaleFireGuard> _logger;
    private readonly AppDataContext _dataContext;

    public StaleFireGuard(
        ILogger<StaleFireGuard> logger,
        AppDataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    public async Task<bool> IsStaleFireAsync(
        Guid userId, string jobKind, Guid targetId, int? seasonWeek, DateTime fireTimeUtc,
        DateTime? waveAnchorUtc = null)
    {
        var row = await _dataContext.PendingScheduledJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j =>
                j.UserId == userId &&
                j.JobKind == jobKind &&
                j.TargetId == targetId &&
                j.SeasonWeek == seasonWeek &&
                j.WaveAnchorUtc == waveAnchorUtc);

        if (row is null)
        {
            _logger.LogInformation(
                "Stale fire: no PendingScheduledJob row for ({JobKind}, {TargetId}, week={SeasonWeek}); aborting.",
                jobKind, targetId, seasonWeek);
            return true;
        }

        if (row.ScheduledFireUtc != fireTimeUtc)
        {
            _logger.LogInformation(
                "Stale fire: scheduler has moved to FireTime={CurrentFireTime}; this job was scheduled for {OrphanFireTime}. Aborting.",
                row.ScheduledFireUtc, fireTimeUtc);
            return true;
        }

        return false;
    }
}
