using System.ComponentModel.DataAnnotations;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Write-only audit row: what we sent, to whom, when, via which channel,
    /// with what outcome. Doubles as the idempotency table — at-least-once
    /// delivery from RabbitMQ means consumers may re-fire; the dedupe key
    /// (CorrelationId + UserId + Channel) makes the send path safe to retry.
    ///
    /// Never queried by the user-facing app — this is for debugging,
    /// rate-limiting, and proving "yes we did send that on date X."
    /// </summary>
    public class NotificationLog : CanonicalEntityBase<Guid>
    {
        public Guid UserId { get; set; }

        /// <summary>
        /// CorrelationId from the originating event. Same value across the
        /// publisher (API / Producer), the consumer (Notification handler),
        /// and the send attempt. Forms part of the idempotency key.
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// Notification category — mirrors <see cref="UserNotificationPreferences"/>
        /// flags. e.g. "PickResult", "PickDeadline", "ContestStart", "LeagueInvite",
        /// "Membership", "MatchupPreview", "ScheduleChange".
        /// </summary>
        [Required]
        [MaxLength(32)]
        public string Category { get; set; }

        /// <summary>
        /// Channel used. Today "Fcm" only; reserved for future "Email", "Sms".
        /// </summary>
        [Required]
        [MaxLength(16)]
        public string Channel { get; set; }

        [MaxLength(256)]
        public string Title { get; set; }

        [MaxLength(1024)]
        public string Body { get; set; }

        /// <summary>
        /// Outcome. e.g. "Sent", "Suppressed_UserOptedOut", "Suppressed_NoDevice",
        /// "Suppressed_StaleFire", "Failed_FcmError", "Skipped_Duplicate".
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string Result { get; set; }

        [MaxLength(512)]
        public string FailureReason { get; set; }

        public DateTime AttemptedUtc { get; set; }
    }
}
