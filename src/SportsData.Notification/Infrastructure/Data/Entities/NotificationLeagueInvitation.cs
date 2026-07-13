using System.ComponentModel.DataAnnotations;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Audit + idempotency row for a <b>league-invite</b> push. Typed
    /// notification table replacing the catch-all <see cref="NotificationLog"/>
    /// for the LeagueInvite category (see
    /// <c>docs/architecture/notification-log-table-per-type.md</c>).
    ///
    /// <para>
    /// Dedup key is <c>(UserId, LeagueId, CorrelationId)</c>. Each invite
    /// <i>action</i> has its own <see cref="CorrelationId"/> (stable across
    /// at-least-once redelivery, distinct across separate invites), so — unlike
    /// the pick-result table where CorrelationId was the wrong key —
    /// correlation-level dedup is <b>correct</b> here: a redelivery of one invite
    /// collides and is suppressed, but a genuine <b>re-invite re-notifies</b>
    /// (decided: the user may have missed the first). If an explicit
    /// <c>InvitationId</c> is ever added to the event, prefer it over
    /// <see cref="CorrelationId"/> in the key.
    /// </para>
    ///
    /// <para>Never read by the user-facing app — audit / debugging / idempotency only.</para>
    /// </summary>
    public class NotificationLeagueInvitation : CanonicalEntityBase<Guid>
    {
        /// <summary>Recipient (the invitee).</summary>
        public Guid UserId { get; set; }

        /// <summary>League (PickemGroup) the invite is to.</summary>
        public Guid LeagueId { get; set; }

        /// <summary>Who sent the invite. Captured for visibility.</summary>
        public Guid InvitedByUserId { get; set; }

        /// <summary>
        /// CorrelationId of the originating invite action. Part of the dedup key
        /// (see class remarks) — distinguishes a genuine re-invite from a
        /// redelivery of the same invite.
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
