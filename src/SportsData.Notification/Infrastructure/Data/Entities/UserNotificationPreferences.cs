using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Per-user, per-category opt-in flags. One row per user. The category set
    /// here mirrors the notification catalog in
    /// <c>docs/architecture/notification-service-events-and-state.md</c> §2.
    /// Defaults assume "everything on" — this is a pick'em product where the
    /// notifications ARE the engagement layer; users explicitly opting out is
    /// the rare path.
    /// </summary>
    public class UserNotificationPreferences : CanonicalEntityBase<Guid>
    {
        public Guid UserId { get; set; }

        public bool PickResultEnabled { get; set; } = true;

        public bool PickDeadlineReminderEnabled { get; set; } = true;

        public bool ContestStartReminderEnabled { get; set; } = true;

        public bool LeagueInviteEnabled { get; set; } = true;

        public bool MembershipEnabled { get; set; } = true;

        public bool MatchupPreviewEnabled { get; set; } = true;

        public bool ScheduleChangeEnabled { get; set; } = true;

        public bool OddsChangedEnabled { get; set; } = true;
    }
}
