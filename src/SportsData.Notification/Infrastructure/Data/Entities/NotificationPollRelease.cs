using System.ComponentModel.DataAnnotations;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Audit + idempotency row for a <b>poll-release</b> push ("AP Top 25 is
    /// out"). First broadcast-class notification: one originating event fans
    /// out to every member of an active league for the poll's sport, so the
    /// claim is per recipient. See
    /// <c>docs/features/poll-release-notifications.md</c>.
    ///
    /// <para>
    /// The dedup key is <c>(UserId, SeasonPollWeekId)</c>: a poll week is
    /// created once, so this guarantees at most one push per user per poll
    /// drop — absorbing at-least-once redelivery today and any future
    /// revision re-publish. <see cref="CorrelationId"/> is retained for
    /// cross-service tracing only and does not participate in uniqueness.
    /// </para>
    ///
    /// <para>
    /// Never read by the user-facing app — audit / debugging / idempotency
    /// only. The atomic-claim dispatch pattern (insert Dispatching → prefs →
    /// devices → send → finalize) matches the other typed tables.
    /// </para>
    /// </summary>
    public class NotificationPollRelease : CanonicalEntityBase<Guid>
    {
        public Guid UserId { get; set; }

        /// <summary>
        /// The poll drop (<c>SeasonPollWeek.Id</c> in Producer). Dedup key
        /// with <see cref="UserId"/> — one poll-release push per user per
        /// poll week, ever.
        /// </summary>
        public Guid SeasonPollWeekId { get; set; }

        /// <summary>Parent poll (<c>SeasonPoll.Id</c>). Captured for visibility / future rollup.</summary>
        public Guid SeasonPollId { get; set; }

        public Sport Sport { get; set; }

        /// <summary>
        /// CorrelationId from the originating <c>SeasonPollWeekCreated</c>
        /// event. Retained for tracing only; NOT part of the dedup key.
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
