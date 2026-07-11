using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// Canonical per-user, per-category notification opt-in flags. One row per
    /// user. The API owns these; changes are projected to the Notification
    /// service (which gates sends) via <c>UserNotificationPreferencesUpdated</c>.
    /// Defaults are "everything on" — notifications are the engagement layer, so
    /// opting out is the rare path. A user with no row is treated as all-enabled;
    /// a row is only created when the user first changes a setting.
    /// See docs/mobile/notification-preferences.md.
    /// </summary>
    public class UserNotificationPreferences : CanonicalEntityBase<Guid>
    {
        public Guid UserId { get; set; }

        public User User { get; set; } = null!;

        public bool PickResultEnabled { get; set; } = true;

        public bool PickDeadlineReminderEnabled { get; set; } = true;

        public bool ContestStartReminderEnabled { get; set; } = true;

        public bool LeagueInviteEnabled { get; set; } = true;

        public bool MembershipEnabled { get; set; } = true;

        public bool MatchupPreviewEnabled { get; set; } = true;

        public bool ScheduleChangeEnabled { get; set; } = true;

        public bool OddsChangedEnabled { get; set; } = true;

        public class EntityConfiguration : IEntityTypeConfiguration<UserNotificationPreferences>
        {
            public void Configure(EntityTypeBuilder<UserNotificationPreferences> builder)
            {
                builder.ToTable(nameof(UserNotificationPreferences));

                builder.HasKey(x => x.Id);

                // One row per user.
                builder.HasIndex(x => x.UserId).IsUnique();

                builder.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
