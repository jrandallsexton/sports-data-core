using System.ComponentModel.DataAnnotations;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Audit + idempotency row for a <b>matchups-ready</b> push ("Week N
    /// matchups are set in {league} — make your picks"). The opening bookend
    /// to the pick-deadline reminder's closing bookend. See
    /// <c>docs/features/poll-release-notifications.md</c>.
    ///
    /// <para>
    /// The dedup key is <c>(UserId, LeagueId, SeasonYear, SeasonWeek)</c>:
    /// ranked-league refresh passes legitimately re-publish
    /// <c>PickemGroupWeekMatchupsGenerated</c> when late contests are
    /// inserted mid-week, and each such event must NOT re-notify — one
    /// "week is ready" push per user per league-week, first insert wins.
    /// <see cref="CorrelationId"/> is retained for cross-service tracing
    /// only and does not participate in uniqueness.
    /// </para>
    ///
    /// <para>
    /// Never read by the user-facing app — audit / debugging / idempotency
    /// only. The atomic-claim dispatch pattern (insert Dispatching → prefs →
    /// devices → send → finalize) matches the other typed tables.
    /// </para>
    /// </summary>
    public class NotificationMatchupsReady : CanonicalEntityBase<Guid>
    {
        public Guid UserId { get; set; }

        /// <summary>League (PickemGroup) whose week slate was generated.</summary>
        public Guid LeagueId { get; set; }

        public int SeasonYear { get; set; }

        public int SeasonWeek { get; set; }

        /// <summary>
        /// CorrelationId from the originating
        /// <c>PickemGroupWeekMatchupsGenerated</c> event. Retained for
        /// tracing only; NOT part of the dedup key.
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
