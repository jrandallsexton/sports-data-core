using System.ComponentModel.DataAnnotations;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Audit + idempotency row for a <b>pick-result</b> push (the notification a
    /// user gets when a contest they picked finalizes). First of the typed
    /// notification tables replacing the catch-all <see cref="NotificationLog"/>
    /// (see <c>docs/architecture/notification-log-table-per-type.md</c>).
    ///
    /// <para>
    /// Unlike <see cref="NotificationLog"/> this table carries the notification's
    /// <b>subject</b> — <see cref="PickId"/>, <see cref="ContestId"/>,
    /// <see cref="LeagueId"/> — as first-class, queryable columns. The dedup key
    /// is <c>(UserId, PickId)</c>: a pick is scored once, so this guarantees at
    /// most one push per pick and stops the cross-run duplicate the old
    /// <c>(CorrelationId, UserId, Channel)</c> key let through, while preserving
    /// the legitimately-distinct per-league notifications that same key wrongly
    /// suppressed. <see cref="CorrelationId"/> is retained for cross-service
    /// tracing only and does not participate in uniqueness.
    /// </para>
    ///
    /// <para>
    /// Never read by the user-facing app — audit / debugging / idempotency only.
    /// The atomic-claim dispatch pattern (insert Dispatching → resolve prefs /
    /// devices → send → finalize) matches <see cref="NotificationLog"/>.
    /// </para>
    /// </summary>
    public class NotificationUserPick : CanonicalEntityBase<Guid>
    {
        public Guid UserId { get; set; }

        /// <summary>
        /// The scored pick (<c>PickemGroupUserPick.Id</c>). Dedup key with
        /// <see cref="UserId"/> — one pick-result push per pick, ever.
        /// </summary>
        public Guid PickId { get; set; }

        /// <summary>Contest the pick was on. Captured for visibility / future rollup.</summary>
        public Guid ContestId { get; set; }

        /// <summary>League (PickemGroup) the pick belongs to. Captured for visibility / future rollup.</summary>
        public Guid LeagueId { get; set; }

        /// <summary>
        /// CorrelationId from the originating <c>UserPickScored</c> event.
        /// Retained for tracing only; NOT part of the dedup key.
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
        /// Outcome. e.g. "Dispatching", "Sent", "Suppressed_UserOptedOut",
        /// "Suppressed_NoDevice", "Failed_FcmError".
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string Result { get; set; }

        [MaxLength(512)]
        public string FailureReason { get; set; }

        public DateTime AttemptedUtc { get; set; }
    }
}
