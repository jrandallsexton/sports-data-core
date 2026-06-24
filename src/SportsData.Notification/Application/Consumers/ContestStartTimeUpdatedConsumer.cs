using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Eventing.Events.Contests;
using SportsData.Notification.Infrastructure.Data;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// ESPN moved a game's start time. For every user who had a kickoff
    /// reminder scheduled for this contest, cancel the existing Hangfire job
    /// and schedule a new one at <c>NewStartTime - leadMinutes</c>.
    ///
    /// <para>
    /// Mirrors Producer's <c>CompetitionStreamScheduler.RescheduleForContestAsync</c>
    /// pattern but operates on the Notification side: same drift threshold
    /// logic, same crash-safe ordering (schedule-new → save → delete-old),
    /// just against the local <see cref="Infrastructure.Data.Entities.PendingScheduledJob"/>
    /// table instead of <c>CompetitionStream</c>.
    /// </para>
    ///
    /// <para>
    /// Per the cross-broker shovel design, this event arrives via a shovel
    /// from each per-sport Producer broker (NCAA / NFL / MLB). The handler
    /// itself is sport-agnostic — it operates entirely off
    /// <c>PendingScheduledJob.TargetId == ContestId</c>.
    /// </para>
    /// </summary>
    public class ContestStartTimeUpdatedConsumer : IConsumer<ContestStartTimeUpdated>
    {
        private readonly ILogger<ContestStartTimeUpdatedConsumer> _logger;
        private readonly AppDataContext _dataContext;

        public ContestStartTimeUpdatedConsumer(
            ILogger<ContestStartTimeUpdatedConsumer> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
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

            var pendingJobs = await _dataContext.PendingScheduledJobs
                .Where(j => j.JobKind == "Kickoff" && j.TargetId == msg.ContestId)
                .ToListAsync();

            if (pendingJobs.Count == 0)
            {
                _logger.LogInformation(
                    "No PendingScheduledJob rows for ContestId {ContestId}; nothing to reschedule.",
                    msg.ContestId);
                return;
            }

            _logger.LogInformation(
                "Rescheduling {Count} kickoff reminder(s) for ContestId {ContestId}.",
                pendingJobs.Count, msg.ContestId);

            foreach (var job in pendingJobs)
            {
                // TODO (Hangfire integration):
                //   1. Compute newFireTime = msg.NewStartTime - leadMinutes
                //   2. newJobId = _backgroundJobProvider.Schedule<INotificationDispatcher>(
                //          d => d.SendKickoffReminderAsync(job.UserId, msg.ContestId),
                //          newFireTime - _dateTimeProvider.UtcNow());
                //   3. oldJobId = job.HangfireJobId
                //   4. job.HangfireJobId = newJobId
                //   5. job.ScheduledFireUtc = newFireTime
                //   6. job.ModifiedUtc = now
                //   7. SaveChanges  (commits the swap)
                //   8. _backgroundJobProvider.Delete(oldJobId)  (best-effort)
                //
                // Crash-safety: schedule-then-save-then-delete order means a
                // crashed run leaks an orphan job rather than orphaning the row.
                // FCM dedupe via NotificationLog (CorrelationId + UserId + Channel)
                // absorbs the duplicate fire.

                _logger.LogInformation(
                    "TODO: reschedule Hangfire job {OldJobId} for UserId {UserId} (ContestId {ContestId}).",
                    job.HangfireJobId, job.UserId, msg.ContestId);
            }

            await _dataContext.SaveChangesAsync();
        }
    }
}
