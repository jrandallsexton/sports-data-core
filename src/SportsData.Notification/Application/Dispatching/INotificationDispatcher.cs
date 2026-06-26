namespace SportsData.Notification.Application.Dispatching
{
    /// <summary>
    /// Hangfire-invoked entry point for time-scheduled push dispatches.
    /// One method per category — PickDeadline (per league-week) and
    /// ContestStart (per contest, sport-aware copy). Each method is
    /// responsible for: resolving the recipient's preferences + devices,
    /// composing the body, dispatching via FCM, and writing the atomic-claim
    /// audit row in <c>NotificationLog</c>.
    ///
    /// <para>
    /// Methods are designed to be safe under Hangfire's at-least-once
    /// invocation semantics — a deterministic CorrelationId derived from
    /// the input parameters keeps the <c>NotificationLog</c> dedupe
    /// constraint catching retries that would otherwise duplicate.
    /// </para>
    /// </summary>
    public interface INotificationDispatcher
    {
        Task SendPickDeadlineReminderAsync(Guid userId, Guid pickemGroupId, int seasonWeek, DateTime fireTimeUtc);

        Task SendContestStartReminderAsync(Guid userId, Guid contestId, DateTime fireTimeUtc);
    }
}
