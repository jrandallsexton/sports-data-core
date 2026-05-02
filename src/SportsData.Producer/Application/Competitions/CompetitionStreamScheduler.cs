using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Competitions;

/// <summary>
/// Sport-neutral recurring job: walks the current SeasonWeek and ensures every
/// competition has an ICompetitionBroadcastingJob scheduled 10 minutes before
/// kickoff. Handles three cases per competition:
///
/// 1. No CompetitionStream row exists → schedule a new job, insert row.
/// 2. Stream exists in Scheduled state, but ESPN moved the game time enough that
///    the existing Hangfire job's fire time is now stale → cancel the old job,
///    schedule a new one, update the row.
/// 3. Otherwise → leave alone (already-correct schedule, live stream in flight,
///    or terminal state).
///
/// Per-sport pods register exactly one ICompetitionBroadcastingJob implementation;
/// this scheduler is sport-coupled only via the IAppMode.CurrentSport stamped on
/// the dispatched command.
/// </summary>
public class CompetitionStreamScheduler
{
    /// <summary>
    /// Reschedule kicks in only when the new desired fire time differs from the
    /// existing one by more than this. Smaller drifts are ignored — they're
    /// usually noise from ESPN's data churn rather than real game-time changes.
    /// </summary>
    public static readonly TimeSpan RescheduleDriftThreshold = TimeSpan.FromMinutes(5);

    /// <summary>
    /// If the existing scheduled job is about to fire (or already firing), we
    /// don't try to cancel and reschedule — the streamer's own status polling
    /// will sort out any small drift on entry. Avoids a race between Delete
    /// and the job's own state transition.
    /// </summary>
    public static readonly TimeSpan AboutToFireGuard = TimeSpan.FromMinutes(1);

    private readonly ILogger<CompetitionStreamScheduler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly IAppMode _appMode;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CompetitionStreamScheduler(
        ILogger<CompetitionStreamScheduler> logger,
        TeamSportDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider,
        IAppMode appMode,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
        _appMode = appMode;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Parameterless overload for Hangfire recurring job registration.
    /// </summary>
    public Task Execute() => ExecuteAsync(CancellationToken.None);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.UtcNow();

        var seasonWeek = await _dataContext.SeasonWeeks
            .AsNoTracking()
            .Where(sw => sw.StartDate <= now && sw.EndDate >= now)
            .FirstOrDefaultAsync(cancellationToken);

        if (seasonWeek == null)
        {
            _logger.LogWarning("No current season week found. Skipping broadcast scheduling.");
            return;
        }

        // Note: Contest.Competitions navigation only exists on sport-specific subtypes
        // (FootballContest, BaseballContest), not on ContestBase. Query Competitions
        // directly and pull SeasonYear from the included Contest.
        var competitions = await _dataContext.Competitions
            .Include(c => c.Contest)
            .Include(c => c.ExternalIds)
            .Where(c => c.Contest.SeasonWeekId == seasonWeek.Id)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        // Tracked load — we may mutate ScheduledTimeUtc / BackgroundJobId on existing rows.
        var existingStreams = await _dataContext.CompetitionStreams
            .Where(x => x.SeasonWeekId == seasonWeek.Id)
            .ToListAsync(cancellationToken);

        var existingByCompetitionId = existingStreams.ToDictionary(s => s.CompetitionId);

        var scheduledCount = 0;
        var rescheduledCount = 0;

        foreach (var competition in competitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var contest = competition.Contest;

            var externalId = competition.ExternalIds.FirstOrDefault(x => x.Provider == SourceDataProvider.Espn);
            if (externalId == null)
            {
                _logger.LogWarning("No ESPN ExternalId found for CompetitionId {CompetitionId}. Skipping.", competition.Id);
                continue;
            }

            var desiredScheduledTimeUtc = ComputeScheduledTimeUtc(competition.Date, now);

            if (existingByCompetitionId.TryGetValue(competition.Id, out var existingStream))
            {
                if (TryReschedule(existingStream, competition, contest, desiredScheduledTimeUtc, now))
                {
                    rescheduledCount++;
                }

                continue;
            }

            var jobId = ScheduleStreamJob(competition, contest, desiredScheduledTimeUtc, now);

            _dataContext.CompetitionStreams.Add(new CompetitionStream
            {
                BackgroundJobId = jobId,
                CompetitionId = competition.Id,
                CreatedBy = Guid.Empty,
                CreatedUtc = now,
                Id = Guid.NewGuid(),
                ScheduledBy = nameof(CompetitionStreamScheduler),
                ScheduledTimeUtc = desiredScheduledTimeUtc,
                SeasonWeekId = seasonWeek.Id,
                Status = CompetitionStreamStatus.Scheduled,
            });

            _logger.LogInformation("Scheduled stream for CompetitionId {CompetitionId} at {ScheduledTimeUtc} with JobId {JobId}.",
                competition.Id, desiredScheduledTimeUtc, jobId);

            scheduledCount++;
        }

        if (scheduledCount > 0 || rescheduledCount > 0)
        {
            await _dataContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Stream scheduling complete for SeasonWeek {SeasonWeekNumber}. New: {ScheduledCount}, Rescheduled: {RescheduledCount}.",
                seasonWeek.Number, scheduledCount, rescheduledCount);
        }
        else
        {
            _logger.LogInformation("No new or rescheduled competition streams needed.");
        }
    }

    private static DateTime ComputeScheduledTimeUtc(DateTime competitionDateUtc, DateTime now)
    {
        var target = competitionDateUtc - TimeSpan.FromMinutes(10);
        return target < now ? now.AddSeconds(5) : target;
    }

    private string ScheduleStreamJob(
        Infrastructure.Data.Entities.CompetitionBase competition,
        Infrastructure.Data.Entities.ContestBase contest,
        DateTime scheduledTimeUtc,
        DateTime now)
    {
        var correlationId = Guid.NewGuid();

        // Note: CancellationToken.None is used here because this is the token that will be passed
        // to the scheduled job when it executes in the future. The scheduled job will have its own
        // cancellation management via Hangfire's job cancellation mechanisms.
        return _backgroundJobProvider.Schedule<ICompetitionBroadcastingJob>(
            job => job.ExecuteAsync(new StreamCompetitionCommand
            {
                ContestId = competition.ContestId,
                CompetitionId = competition.Id,
                Sport = _appMode.CurrentSport,
                SeasonYear = contest.SeasonYear,
                DataProvider = SourceDataProvider.Espn,
                CorrelationId = correlationId
            }, CancellationToken.None),
            scheduledTimeUtc - now);
    }

    private bool TryReschedule(
        CompetitionStream existing,
        Infrastructure.Data.Entities.CompetitionBase competition,
        Infrastructure.Data.Entities.ContestBase contest,
        DateTime desiredScheduledTimeUtc,
        DateTime now)
    {
        // Only Scheduled streams are reschedulable. AwaitingStart/Active streams
        // are already running (or about to run) on a Hangfire worker; the
        // streamer's own status polling will detect game state. Completed/Failed
        // streams are terminal — leave alone.
        if (existing.Status != CompetitionStreamStatus.Scheduled)
        {
            _logger.LogDebug(
                "CompetitionId {CompetitionId} stream is in {Status} state. Not eligible for reschedule.",
                competition.Id, existing.Status);
            return false;
        }

        // Don't race the Hangfire scheduler — if the existing job is about to
        // fire (or already firing), let it run and sort out drift on entry.
        if (existing.ScheduledTimeUtc - now <= AboutToFireGuard)
        {
            _logger.LogDebug(
                "CompetitionId {CompetitionId} job fires within {Guard}; skipping reschedule.",
                competition.Id, AboutToFireGuard);
            return false;
        }

        var drift = (desiredScheduledTimeUtc - existing.ScheduledTimeUtc).Duration();
        if (drift <= RescheduleDriftThreshold)
        {
            _logger.LogDebug(
                "CompetitionId {CompetitionId} drift is {Drift}, within threshold {Threshold}. No reschedule.",
                competition.Id, drift, RescheduleDriftThreshold);
            return false;
        }

        var oldJobId = existing.BackgroundJobId;
        var oldScheduledTime = existing.ScheduledTimeUtc;

        var deleted = _backgroundJobProvider.Delete(oldJobId);
        if (!deleted)
        {
            // Hangfire refused the state transition — most commonly because the
            // job has already moved to Processing/Succeeded/Deleted. Log and
            // skip; the streamer (or a future scheduler pass) will reconcile.
            _logger.LogWarning(
                "Failed to delete existing Hangfire job {JobId} for CompetitionId {CompetitionId}. Skipping reschedule.",
                oldJobId, competition.Id);
            return false;
        }

        var newJobId = ScheduleStreamJob(competition, contest, desiredScheduledTimeUtc, now);

        existing.BackgroundJobId = newJobId;
        existing.ScheduledTimeUtc = desiredScheduledTimeUtc;
        existing.ModifiedUtc = now;
        existing.ModifiedBy = Guid.Empty;
        existing.Notes = TruncateNote(
            $"Rescheduled {now:O}: {oldScheduledTime:O} → {desiredScheduledTimeUtc:O} (old job {oldJobId})");

        _logger.LogInformation(
            "Rescheduled stream for CompetitionId {CompetitionId} from {OldTime} to {NewTime}. OldJobId={OldJobId}, NewJobId={NewJobId}.",
            competition.Id, oldScheduledTime, desiredScheduledTimeUtc, oldJobId, newJobId);

        return true;
    }

    private static string TruncateNote(string note)
        => note.Length > 1024 ? note[..1024] : note;
}
