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
                await ScheduleOrRescheduleAsync(userId, pickemGroupId, seasonWeek, fireTime, existing, now, cancellationToken);
            }
        }

        private async Task ScheduleOrRescheduleAsync(
            Guid userId,
            Guid pickemGroupId,
            int seasonWeek,
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
            var delay = fireTime - now;
            var newJobId = _backgroundJobProvider.Schedule<INotificationDispatcher>(
                d => d.SendPickDeadlineReminderAsync(userId, pickemGroupId, seasonWeek),
                delay);

            if (existing is null)
            {
                _dataContext.PendingScheduledJobs.Add(new PendingScheduledJob
                {
                    UserId = userId,
                    JobKind = JobKind,
                    TargetId = pickemGroupId,
                    SeasonWeek = seasonWeek,
                    HangfireJobId = newJobId,
                    ScheduledFireUtc = fireTime,
                    CreatedUtc = now,
                    CreatedBy = Guid.Empty
                });
                await _dataContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                var oldJobId = existing.HangfireJobId;
                existing.HangfireJobId = newJobId;
                existing.ScheduledFireUtc = fireTime;
                existing.ModifiedUtc = now;
                await _dataContext.SaveChangesAsync(cancellationToken);

                // Best-effort cancellation of the old Hangfire job. If it
                // already fired or is already deleted, Delete returns false
                // and we don't care — the dispatcher dedupe still protects
                // against a double send.
                try
                {
                    _backgroundJobProvider.Delete(oldJobId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to delete old Hangfire job {OldJobId} after reschedule. Will be absorbed by dispatcher dedupe.",
                        oldJobId);
                }
            }

            _logger.LogInformation(
                "Scheduled PickDeadline reminder. UserId={UserId}, FireTime={FireTime}, HangfireJobId={HangfireJobId}",
                userId, fireTime, newJobId);
        }
    }
}
