using System.ComponentModel.DataAnnotations;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// Local read-side projection of API's PickemGroupMatchup. Seeded via the
    /// <c>PickemGroupMatchupsRequested</c> → <c>PickemGroupMatchupDataPublished</c>
    /// backfill chain (future matchups only — picks have already locked on
    /// past games). Kept fresh by:
    /// <list type="bullet">
    ///   <item>Steady-state <c>PickemGroupMatchupCreated</c> when API adds a
    ///   new matchup to a league.</item>
    ///   <item>Steady-state <c>ContestStartTimeUpdated</c> when Producer
    ///   detects an ESPN start-time change (updates <see cref="StartDateUtc"/>
    ///   on all rows for the contest).</item>
    /// </list>
    ///
    /// <para>
    /// <see cref="StatusTypeName"/> is intentionally defaulted to
    /// <c>"STATUS_SCHEDULED"</c> at insert. API doesn't store canonical
    /// contest status; future <c>ContestStatusChanged</c> consumption updates
    /// this column over time (Phase 2c-main / 2d). Status defaulting is safe
    /// in practice because the backfill filter is
    /// <c>StartDateUtc &gt; UtcNow</c> — any matchup we project hasn't
    /// started yet, so it really is STATUS_SCHEDULED at insert.
    /// </para>
    /// </summary>
    public class PickemGroupMatchup : CanonicalEntityBase<Guid>
    {
        public Guid PickemGroupId { get; set; }

        public Guid ContestId { get; set; }

        public DateTime StartDateUtc { get; set; }

        public int SeasonYear { get; set; }

        public int SeasonWeek { get; set; }

        [Required]
        [MaxLength(50)]
        public string StatusTypeName { get; set; }

        /// <summary>
        /// EventBase.CreatedUtc of the last event that wrote
        /// <see cref="StartDateUtc"/>. Used as a monotonic version key to
        /// reject stale <c>ContestStartTimeUpdated</c> events under
        /// out-of-order delivery — only events with a newer CreatedUtc
        /// can update StartDateUtc. Null on rows that haven't yet been
        /// touched by an update event (first write sets this).
        /// </summary>
        public DateTime? StartDateUpdatedAt { get; set; }
    }
}
