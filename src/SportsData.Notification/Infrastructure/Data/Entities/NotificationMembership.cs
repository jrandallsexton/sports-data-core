using System.ComponentModel.DataAnnotations;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Audit + idempotency row for a <b>membership</b> push (the welcome a user
    /// gets when they join a league). Typed notification table replacing the
    /// catch-all <see cref="NotificationLog"/> for the Membership category (see
    /// <c>docs/architecture/notification-log-table-per-type.md</c>).
    ///
    /// <para>
    /// Dedup key is <c>(UserId, LeagueId)</c> — one welcome per user per league.
    /// A re-add after leaving is rare and would be suppressed; if re-joining
    /// should re-notify, add a qualifier (mirrors the invite table's
    /// CorrelationId approach). <see cref="CorrelationId"/> is retained for
    /// tracing only and is not part of the key.
    /// </para>
    ///
    /// <para>Never read by the user-facing app — audit / debugging / idempotency only.</para>
    /// </summary>
    public class NotificationMembership : CanonicalEntityBase<Guid>
    {
        /// <summary>Recipient (the joining user).</summary>
        public Guid UserId { get; set; }

        /// <summary>League (PickemGroup) joined.</summary>
        public Guid LeagueId { get; set; }

        /// <summary>
        /// CorrelationId from the originating event. Retained for tracing only;
        /// NOT part of the dedup key.
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
