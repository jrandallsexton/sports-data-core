using System.ComponentModel.DataAnnotations;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Local read-side projection of the API's User aggregate. Notification
    /// keeps a minimal copy of the fields it needs to render notification
    /// copy (DisplayName for personalization, Email for the future email
    /// channel, Timezone for time-formatted bodies).
    ///
    /// <para>
    /// Populated via two paths: the one-off backfill (<c>UsersRequested</c>
    /// → API publishes <c>UserDataPublished</c> per user) and any
    /// steady-state user-lifecycle event that lands later. The consumer
    /// upserts by <see cref="CanonicalEntityBase{T}.Id"/> = UserId so both
    /// paths converge on the same row.
    /// </para>
    ///
    /// <para>
    /// FirebaseUid, IsAdmin, IsSynthetic, etc. live on the API's User entity
    /// but are intentionally NOT projected here — Notification has no
    /// authorization or identity-resolution responsibilities; those stay
    /// in API.
    /// </para>
    /// </summary>
    public class User : CanonicalEntityBase<Guid>
    {
        [Required]
        [MaxLength(100)]
        public string DisplayName { get; set; }

        [Required]
        [MaxLength(256)]
        public string Email { get; set; }

        [MaxLength(100)]
        public string Timezone { get; set; }
    }
}
