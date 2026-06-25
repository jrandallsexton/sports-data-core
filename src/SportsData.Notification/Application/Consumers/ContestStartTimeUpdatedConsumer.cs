using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Notification.Infrastructure.Data;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// ESPN moved a game's start time. Two responsibilities:
    /// <list type="number">
    ///   <item>Update the local <c>PickemGroupMatchup</c> projection — every
    ///   row referencing the contest (a single contest can appear in many
    ///   leagues) gets its <c>StartDateUtc</c> resynced via
    ///   <c>ExecuteUpdateAsync</c>.</item>
    ///   <item>Reschedule any <c>PendingScheduledJob</c> kickoff reminders
    ///   already on the Hangfire calendar for this contest (TODO — Phase 2c-main
    ///   / 2d when the Hangfire dispatcher exists).</item>
    /// </list>
    ///
    /// <para>
    /// Mirrors Producer's
    /// <c>CompetitionStreamScheduler.RescheduleForContestAsync</c> pattern for
    /// the Hangfire side: same drift threshold logic, same crash-safe ordering
    /// (schedule-new → save → delete-old) once wired.
    /// </para>
    ///
    /// <para>
    /// Per the cross-broker shovel design, this event arrives via a shovel
    /// from each per-sport Producer broker (NCAA / NFL / MLB). The handler
    /// itself is sport-agnostic — both the projection write and the future
    /// Hangfire reschedule operate entirely off ContestId.
    /// </para>
    /// </summary>
    public class ContestStartTimeUpdatedConsumer : IConsumer<ContestStartTimeUpdated>
    {
        private readonly ILogger<ContestStartTimeUpdatedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;

        public ContestStartTimeUpdatedConsumer(
            ILogger<ContestStartTimeUpdatedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Consume(ConsumeContext<ContestStartTimeUpdated> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["ContestId"] = msg.ContestId,
                ["NewStartTime"] = msg.NewStartTime,
                ["Sport"] = msg.Sport
            });

            _logger.LogInformation("ContestStartTimeUpdated received.");

            // 1. Resync the local PickemGroupMatchup projection. Bulk
            // ExecuteUpdateAsync avoids loading + tracking; ModifiedUtc /
            // ModifiedBy / StartDateUpdatedAt stamped in the SET clause.
            //
            // Out-of-order delivery guard: only apply when the event's
            // CreatedUtc is newer than the projection's last-applied stamp.
            // Producer's CompetitionStreamScheduler solved this for itself
            // by reading from DB (PR #457); we solve it here by versioning
            // the projection field with the event's own monotonic timestamp.
            // The check lives in the WHERE clause so stale events are
            // filtered at the SQL layer with no read round-trip.
            var now = _dateTimeProvider.UtcNow();
            var eventCreatedUtc = msg.CreatedUtc;
            var rowsAffected = await _dataContext.PickemGroupMatchups
                .Where(m =>
                    m.ContestId == msg.ContestId &&
                    (m.StartDateUpdatedAt == null || m.StartDateUpdatedAt < eventCreatedUtc))
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(m => m.StartDateUtc, msg.NewStartTime)
                        .SetProperty(m => m.StartDateUpdatedAt, (DateTime?)eventCreatedUtc)
                        .SetProperty(m => m.ModifiedUtc, (DateTime?)now)
                        .SetProperty(m => m.ModifiedBy, (Guid?)msg.CausationId),
                    context.CancellationToken);

            _logger.LogInformation(
                "PickemGroupMatchup projection resynced. RowsAffected={RowsAffected}, EventCreatedUtc={EventCreatedUtc}",
                rowsAffected, eventCreatedUtc);

            // 2. Reschedule any kickoff-reminder Hangfire jobs already on the
            // calendar for this contest. Today this is TODO pending the
            // Hangfire dispatcher (Phase 2c-main / 2d). For now we log the
            // intent so the recovery path is auditable.
            var pendingJobs = await _dataContext.PendingScheduledJobs
                .Where(j => j.JobKind == "Kickoff" && j.TargetId == msg.ContestId)
                .ToListAsync(context.CancellationToken);

            if (pendingJobs.Count == 0)
            {
                _logger.LogInformation(
                    "No PendingScheduledJob rows for ContestId {ContestId}; no kickoff reminders to reschedule.",
                    msg.ContestId);
                return;
            }

            _logger.LogInformation(
                "TODO (Phase 2c-main / 2d): reschedule {Count} kickoff reminder(s) for ContestId {ContestId}.",
                pendingJobs.Count, msg.ContestId);

            foreach (var job in pendingJobs)
            {
                // TODO (Hangfire integration):
                //   1. Compute newFireTime = msg.NewStartTime - leadMinutes
                //   2. newJobId = _backgroundJobProvider.Schedule<INotificationDispatcher>(
                //          d => d.SendKickoffReminderAsync(job.UserId, msg.ContestId),
                //          newFireTime - now);
                //   3. oldJobId = job.HangfireJobId
                //   4. job.HangfireJobId = newJobId
                //   5. job.ScheduledFireUtc = newFireTime
                //   6. job.ModifiedUtc = now
                //   7. SaveChanges  (commits the swap)
                //   8. _backgroundJobProvider.Delete(oldJobId)  (best-effort)
                //
                // Crash-safety: schedule-then-save-then-delete order leaks an
                // orphan job rather than orphaning the row on crash. FCM dedupe
                // via NotificationLog (CorrelationId + UserId + Channel) absorbs
                // the duplicate fire.
                _logger.LogDebug(
                    "Would reschedule Hangfire job {OldJobId} for UserId {UserId}.",
                    job.HangfireJobId, job.UserId);
            }
        }
    }
}
