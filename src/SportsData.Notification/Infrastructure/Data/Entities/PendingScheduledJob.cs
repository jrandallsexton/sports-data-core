using System.ComponentModel.DataAnnotations;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Tracks Hangfire job ids for time-based notifications (pick-deadline
    /// reminder, contest-start reminder). The row exists so we can find +
    /// cancel the prior Hangfire job when an upstream event reschedules the
    /// trigger — e.g. ContestStartTimeUpdated moves a game earlier, the
    /// existing contest-start-reminder job has to be cancelled and a new
    /// one scheduled.
    ///
    /// Same crash-safe pattern as Producer's CompetitionStream: persist the
    /// new Hangfire job id, save, then delete the old job. If delete fails
    /// the orphan job becomes a benign duplicate — the FCM send path is
    /// idempotent via the reminder tables' natural keys
    /// (NotificationPickDeadline / NotificationContestStart), whose FireTimeUtc
    /// component also lets a reschedule re-fire while a retry does not.
    /// </summary>
    public class PendingScheduledJob : CanonicalEntityBase<Guid>
    {
        public Guid UserId { get; set; }

        /// <summary>
        /// Discriminator for the kind of scheduled notification. Limited string
        /// rather than an enum so adding a category doesn't require a Core change.
        /// Values today: "PickDeadline", "ContestStart".
        /// </summary>
        [Required]
        [MaxLength(32)]
        public string JobKind { get; set; }

        /// <summary>
        /// Logical target the reminder is about. For "ContestStart" this is the
        /// ContestId; for "PickDeadline" this is the PickemGroupId (paired
        /// with <see cref="SeasonWeek"/> in the unique constraint so the
        /// same league can have one row per week).
        /// </summary>
        public Guid TargetId { get; set; }

        /// <summary>
        /// Only meaningful for <c>JobKind = "PickDeadline"</c>. Null for
        /// ContestStart jobs (those are scoped to a single contest, not a week).
        /// Part of the natural key for PickDeadline rows so a league with
        /// matchups generated weeks ahead can carry one scheduled-job row
        /// per upcoming week without collisions.
        /// </summary>
        public int? SeasonWeek { get; set; }

        [Required]
        [MaxLength(64)]
        public string HangfireJobId { get; set; }

        public DateTime ScheduledFireUtc { get; set; }
    }
}
