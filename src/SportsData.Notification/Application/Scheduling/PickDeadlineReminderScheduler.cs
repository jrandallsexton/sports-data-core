using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Scheduling
{
    public interface IPickDeadlineReminderScheduler
    {
        Task EvaluateAndScheduleForLeagueWeekAsync(Guid pickemGroupId, int seasonWeek, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Decides whether to schedule (or reschedule, or no-op) a pick-deadline
    /// reminder for every member of a given league-week. The trigger is
    /// "this league-week's matchup state may have changed" — fired by:
    /// <list type="bullet">
    ///   <item><c>PickemGroupMatchupCreatedConsumer</c> after upserting a new
    ///   matchup from the steady-state event.</item>
    ///   <item><c>PickemGroupMatchupDataPublishedConsumer</c> after upserting
    ///   a matchup from the operator-triggered backfill.</item>
    ///   <item><c>ContestStartTimeUpdatedConsumer</c> after resyncing
    ///   <c>StartDateUtc</c> for matchups referencing the changed contest.</item>
    /// </list>
    ///
    /// <para>
    /// Computes the deadline as <c>MIN(StartDateUtc)</c> of all matchups in
    /// the (group, week) — picks lock when the first game starts. Fires at
    /// <c>deadline - leadTime</c> (currently 1 hour, hardcoded for v1).
    /// </para>
    ///
    /// <para>
    /// Per-user, the scheduler walks the <c>PendingScheduledJob</c> entries
    /// for <c>(UserId, "PickDeadline", PickemGroupId, SeasonWeek)</c> and:
    /// <list type="bullet">
    ///   <item>Inserts + schedules if no row exists and the deadline is still
    ///   in the future.</item>
    ///   <item>Reschedules (schedule-new → save → delete-old, same crash-safe
    ///   ordering as Producer's <c>CompetitionStreamScheduler</c>) when the
    ///   deadline has moved.</item>
    ///   <item>No-ops when the deadline is unchanged or already past.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class PickDeadlineReminderScheduler : IPickDeadlineReminderScheduler
    {
        // Hardcoded for v1 — per the design discussion. Will become a
        // per-user pref later when we surface notification timing controls.
        private static readonly TimeSpan PickDeadlineLeadTime = TimeSpan.FromHours(1);

        private const string JobKind = "PickDeadline";

        private readonly ILogger<PickDeadlineReminderScheduler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public PickDeadlineReminderScheduler(
            ILogger<PickDeadlineReminderScheduler> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task EvaluateAndScheduleForLeagueWeekAsync(
            Guid pickemGroupId,
            int seasonWeek,
            CancellationToken cancellationToken)
        {
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["PickemGroupId"] = pickemGroupId,
                ["SeasonWeek"] = seasonWeek
            });

            // Compute deadline from current DB state. Null = no matchups
            // for this league-week (e.g., matchups got deleted) → nothing
            // to schedule, but if existing rows reference this scope they
            // should arguably be cancelled. v1 leaves them alone; the
            // dispatcher's prefs/device gates will absorb a stale fire.
            var deadline = await _dataContext.PickemGroupMatchups
                .Where(m => m.PickemGroupId == pickemGroupId && m.SeasonWeek == seasonWeek)
                .Select(m => (DateTime?)m.StartDateUtc)
                .MinAsync(cancellationToken);

            if (deadline is null)
            {
                _logger.LogDebug("No matchups for league-week; nothing to schedule.");
                return;
            }

            var fireTime = deadline.Value - PickDeadlineLeadTime;
            var now = _dateTimeProvider.UtcNow();

            if (fireTime <= now)
            {
                // Deadline already past or within the lead window — too late
                // to schedule a "soon" reminder. We could send-immediate
                // instead, but v1 just skips: the user is presumably already
                // making picks if they're going to.
                _logger.LogInformation(
                    "Deadline {Deadline} is past or within lead window from now {Now}; skipping schedule.",
                    deadline, now);
                return;
            }

            // Resolve members of the league. The local PickemGroupMember
            // projection is the source of truth.
            var memberIds = await _dataContext.PickemGroupMembers
                .AsNoTracking()
                .Where(m => m.PickemGroupId == pickemGroupId)
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken);

            if (memberIds.Count == 0)
            {
                _logger.LogDebug("No members for league; nothing to schedule.");
                return;
            }

            _logger.LogInformation(
                "Evaluating PickDeadline reminders for {MemberCount} members. Deadline={Deadline}, FireTime={FireTime}",
                memberIds.Count, deadline, fireTime);

            // Pull existing jobs for the whole league-week in one query —
            // cheaper than per-user round trips for a 30-person league.
            var existingByUser = await _dataContext.PendingScheduledJobs
                .Where(j => j.JobKind == JobKind
                            && j.TargetId == pickemGroupId
                            && j.SeasonWeek == seasonWeek)
                .ToDictionaryAsync(j => j.UserId, cancellationToken);

            // Skip the per-user prefs lookup inside the loop by batching.
            var optedOutUserIds = await _dataContext.UserNotificationPreferences
                .AsNoTracking()
                .Where(p => memberIds.Contains(p.UserId) && !p.PickDeadlineReminderEnabled)
                .Select(p => p.UserId)
                .ToListAsync(cancellationToken);
            var optedOut = optedOutUserIds.ToHashSet();

            foreach (var userId in memberIds)
            {
                if (optedOut.Contains(userId))
                {
                    // Honor opted-out users by NOT scheduling. If a stale row
                    // exists for them from a prior pass (they opted out
                    // after), leave it — the dispatcher's prefs gate will
                    // suppress the fire and audit it.
                    continue;
                }

                existingByUser.TryGetValue(userId, out var existing);
                await ScheduleOrRescheduleAsync(
                    userId, pickemGroupId, seasonWeek, deadline.Value, fireTime, existing, now, cancellationToken);
            }
        }

        private async Task ScheduleOrRescheduleAsync(
            Guid userId,
            Guid pickemGroupId,
            int seasonWeek,
            DateTime deadlineUtc,
            DateTime fireTime,
            PendingScheduledJob existing,
            DateTime now,
            CancellationToken cancellationToken)
        {
            if (existing is not null && existing.ScheduledFireUtc == fireTime)
            {
                // Deadline hasn't moved — no work.
                return;
            }

            // Schedule the new job FIRST, then persist, then delete the old.
            // Crash-safe ordering: a crash between schedule and save leaks
            // an orphan Hangfire job (benign — dispatcher's dedupe absorbs
            // it). The alternative (delete-old first) could leave a row
            // pointing at a deleted job, silently missing the reminder.
            // deadlineUtc is forwarded so the dispatcher's deterministic
            // CorrelationId can include the version anchor. A deadline shift
            // (new earliest matchup) yields a different key per fire-time,
            // so an orphan from a failed best-effort TryDelete can't claim
            // the NotificationLog slot and suppress the new reminder.
            var delay = fireTime - now;
            var newJobId = _backgroundJobProvider.Schedule<INotificationDispatcher>(
                d => d.SendPickDeadlineReminderAsync(userId, pickemGroupId, seasonWeek, deadlineUtc),
                delay);

            if (existing is null)
            {
                var entity = new PendingScheduledJob
                {
                    UserId = userId,
                    JobKind = JobKind,
                    TargetId = pickemGroupId,
                    SeasonWeek = seasonWeek,
                    HangfireJobId = newJobId,
                    ScheduledFireUtc = fireTime,
                    CreatedUtc = now,
                    CreatedBy = Guid.Empty
                };
                _dataContext.PendingScheduledJobs.Add(entity);

                try
                {
                    await _dataContext.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    // A peer scheduler run (concurrent consumer for the same
                    // league-week) inserted first. Detach our orphan, fetch
                    // the winner's row, and continue down the reschedule
                    // branch — if the peer scheduled the same fireTime we
                    // no-op below; otherwise we reschedule. The Hangfire job
                    // we just scheduled becomes the orphan to delete.
                    _dataContext.Entry(entity).State = EntityState.Detached;
                    var winner = await _dataContext.PendingScheduledJobs
                        .FirstAsync(j => j.UserId == userId
                                         && j.JobKind == JobKind
                                         && j.TargetId == pickemGroupId
                                         && j.SeasonWeek == seasonWeek,
                                    cancellationToken);

                    if (winner.ScheduledFireUtc == fireTime)
                    {
                        // Same fireTime — peer already covered it. Clean up
                        // our orphan and exit.
                        TryDeleteHangfireJob(newJobId);
                        return;
                    }

                    // Different fireTime — we take over the scheduling. Old
                    // jobId is the winner's; new is ours.
                    var winnerOldJobId = winner.HangfireJobId;
                    winner.HangfireJobId = newJobId;
                    winner.ScheduledFireUtc = fireTime;
                    winner.ModifiedUtc = now;
                    await _dataContext.SaveChangesAsync(cancellationToken);
                    TryDeleteHangfireJob(winnerOldJobId);
                    _logger.LogInformation(
                        "Concurrent insert resolved; took over scheduling. UserId={UserId}, FireTime={FireTime}",
                        userId, fireTime);
                    return;
                }
            }
            else
            {
                var oldJobId = existing.HangfireJobId;
                existing.HangfireJobId = newJobId;
                existing.ScheduledFireUtc = fireTime;
                existing.ModifiedUtc = now;
                await _dataContext.SaveChangesAsync(cancellationToken);

                TryDeleteHangfireJob(oldJobId);
            }

            _logger.LogInformation(
                "Scheduled PickDeadline reminder. UserId={UserId}, FireTime={FireTime}, HangfireJobId={HangfireJobId}",
                userId, fireTime, newJobId);
        }

        // Best-effort cancellation. Hangfire returns false if the job is
        // already in a terminal state — the dispatcher's NotificationLog
        // dedupe absorbs any duplicate fire from a missed delete.
        private void TryDeleteHangfireJob(string jobId)
        {
            try
            {
                _backgroundJobProvider.Delete(jobId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to delete Hangfire job {JobId}. Absorbed by dispatcher dedupe.",
                    jobId);
            }
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
