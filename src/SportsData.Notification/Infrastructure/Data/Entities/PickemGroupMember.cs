using System.ComponentModel.DataAnnotations;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Membership join row — which users belong to which leagues. Drives
    /// league-wide fan-out queries ("for every member of league X, look up
    /// devices and dispatch"). Backfill consumer replaces all members for a
    /// given GroupId on each PickemGroupDataPublished arrival, so repeated
    /// backfills converge on the source-of-truth set.
    /// </summary>
    public class PickemGroupMember : CanonicalEntityBase<Guid>
    {
        public Guid PickemGroupId { get; set; }

        public Guid UserId { get; set; }

        /// <summary>
        /// "Commissioner" | "Member" (string form of API's LeagueRole). Avoid
        /// taking a dependency on the API enum cross-service.
        /// </summary>
        [Required]
        [MaxLength(32)]
        public string Role { get; set; }
    }
}
