using System.ComponentModel.DataAnnotations;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Audit + idempotency row for a <b>pick-deadline reminder</b> push. Typed
    /// notification table replacing the catch-all <see cref="NotificationLog"/>
    /// for the PickDeadline category (see
    /// <c>docs/architecture/notification-log-table-per-type.md</c>).
    ///
    /// <para>
    /// Dedup key is <c>(UserId, LeagueId, SeasonWeek, FireTimeUtc)</c>. The
    /// <see cref="FireTimeUtc"/> component preserves the scheduler's fire-time
    /// versioning: a Hangfire retry of the same fire collides and is suppressed,
    /// but a reschedule (new fire-time) is a new row and re-fires. This replaces
    /// the deterministic-CorrelationId trick the dispatcher used against
    /// NotificationLog; <see cref="CorrelationId"/> is now a stable trace id only,
    /// not part of the key.
    /// </para>
    ///
    /// <para>Never read by the user-facing app — audit / debugging / idempotency only.</para>
    /// </summary>
    public class NotificationPickDeadline : CanonicalEntityBase<Guid>
    {
        /// <summary>Recipient.</summary>
        public Guid UserId { get; set; }

        /// <summary>League (PickemGroup) whose pick deadline is approaching.</summary>
        public Guid LeagueId { get; set; }

        /// <summary>Season week the deadline is for. Part of the dedup key.</summary>
        public int SeasonWeek { get; set; }

        /// <summary>
        /// The scheduler's intended fire time — the version anchor. Part of the
        /// dedup key so a reschedule re-fires while a retry of the same fire does not.
        /// </summary>
        public DateTime FireTimeUtc { get; set; }

        /// <summary>
        /// Deterministic trace id derived from the reminder's parameters. Stable
        /// across retries for log correlation; NOT part of the dedup key.
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>Channel used. Today "Fcm" only; reserved for future "Email", "Sms".</summary>
        [Required]
        [MaxLength(16)]
        public string Channel { get; set; }

        [MaxLength(256)]
        public string Title { get; set; }

        [MaxLength(1024)]
        public string Body { get; set; }

        /// <summary>
        /// Outcome. e.g. "Dispatching", "Sent", "Suppressed_StaleFire",
        /// "Suppressed_UserOptedOut", "Suppressed_NoDevice", "Failed_FcmError".
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string Result { get; set; }

        [MaxLength(512)]
        public string FailureReason { get; set; }

        public DateTime AttemptedUtc { get; set; }
    }
}
