using System.ComponentModel.DataAnnotations;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Local read-side projection of API's PickemGroup. Seeded via the
    /// PickemGroupsRequested → PickemGroupDataPublished backfill chain.
    ///
    /// <para>
    /// Notification needs Name (notification copy: "You joined {Name}"),
    /// Sport (so future per-sport notification logic can branch), and
    /// CommissionerUserId (commissioner-side fan-out: "{NewMember} joined
    /// your league").
    /// </para>
    /// </summary>
    public class PickemGroup : CanonicalEntityBase<Guid>
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        public Sport Sport { get; set; }

        public Guid CommissionerUserId { get; set; }
    }
}
