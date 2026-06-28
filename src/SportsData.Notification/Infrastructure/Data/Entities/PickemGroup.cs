using System.ComponentModel.DataAnnotations;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Local read-side projection of API's PickemGroup. Seeded at creation via
    /// PickemGroupCreated and kept current via the PickemGroupsRequested →
    /// PickemGroupDataPublished backfill chain.
    ///
    /// <para>
    /// Notification needs Name (notification copy: "You joined {Name}"),
    /// Sport (so future per-sport notification logic can branch),
    /// CommissionerUserId (commissioner-side fan-out: "{NewMember} joined
    /// your league"), and PickType (so line-move notifications target only
    /// leagues whose scoring depends on the odds — ATS cares about spread,
    /// OverUnder about the total, StraightUp about neither).
    /// </para>
    /// </summary>
    public class PickemGroup : CanonicalEntityBase<Guid>
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        public Sport Sport { get; set; }

        public Guid CommissionerUserId { get; set; }

        /// <summary>
        /// String form of API's PickType enum (see <see cref="LeaguePickType"/>).
        /// Defaults to the odds-agnostic StraightUp so a row that predates this
        /// field never over-notifies until a backfill refreshes it.
        /// </summary>
        [Required]
        [MaxLength(40)]
        public string PickType { get; set; } = LeaguePickType.StraightUp;
    }
}
