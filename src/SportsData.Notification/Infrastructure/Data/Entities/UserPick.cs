using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Projection of an active user pick, fed by the <c>UserPickMade</c> event.
    /// Lets the Notification service answer "who picked contest C" so contest-level
    /// events (e.g. odds/line moves) can be targeted at actual pickers instead of
    /// all league members. One row per (UserId, ContestId, PickemGroupId) — a user
    /// can pick the same contest in multiple leagues.
    /// </summary>
    public class UserPick : CanonicalEntityBase<Guid>
    {
        public Guid UserId { get; set; }

        public Guid ContestId { get; set; }

        public Guid PickemGroupId { get; set; }
    }
}
