using System.ComponentModel.DataAnnotations;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Tracks Hangfire job ids for time-based notifications (pick deadline
    /// reminder, kickoff reminder). The row exists so we can find + cancel
    /// the prior Hangfire job when an upstream event reschedules the trigger
    /// — e.g. ContestStartTimeUpdated moves a game earlier, the existing
    /// kickoff-reminder job has to be cancelled and a new one scheduled.
    ///
    /// Same crash-safe pattern as Producer's CompetitionStream: persist the
    /// new Hangfire job id, save, then delete the old job. If delete fails
    /// the orphan job becomes a benign duplicate — the FCM send path is
    /// idempotent via the NotificationLog dedupe key.
    /// </summary>
    public class PendingScheduledJob : CanonicalEntityBase<Guid>
    {
        public Guid UserId { get; set; }

        /// <summary>
        /// Discriminator for the kind of scheduled notification. Limited string
        /// rather than an enum so adding a category doesn't require a Core change.
        /// Values today: "PickDeadline", "Kickoff".
        /// </summary>
        [Required]
        [MaxLength(32)]
        public string JobKind { get; set; }

        /// <summary>
        /// Logical target the reminder is about. For "Kickoff" this is the
        /// ContestId; for "PickDeadline" this is the PickemGroupWeek row id
        /// (one deadline reminder per user per league per week).
        /// </summary>
        public Guid TargetId { get; set; }

        [Required]
        [MaxLength(64)]
        public string HangfireJobId { get; set; }

        public DateTime ScheduledFireUtc { get; set; }
    }
}
