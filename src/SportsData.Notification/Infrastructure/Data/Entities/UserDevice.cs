using System.ComponentModel.DataAnnotations;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Per-user, per-device FCM token registration. Mobile POSTs to Notification's
    /// own API at sign-in (after the iOS / Android permission prompt) and again on
    /// token refresh. Multiple rows per user are normal — one phone, one iPad, etc.
    /// Master <see cref="NotificationsEnabled"/> reflects whether the user wants
    /// notifications at all; per-category opt-outs live on
    /// <see cref="UserNotificationPreferences"/>.
    /// </summary>
    public class UserDevice : CanonicalEntityBase<Guid>
    {
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(256)]
        public string FcmToken { get; set; }

        /// <summary>
        /// "ios" or "android". Stored as a string rather than an enum to keep the
        /// Notification service decoupled from any platform enum that might live
        /// in another project.
        /// </summary>
        [Required]
        [MaxLength(16)]
        public string Platform { get; set; }

        public bool NotificationsEnabled { get; set; } = true;

        public DateTime LastSeenUtc { get; set; }
    }
}
