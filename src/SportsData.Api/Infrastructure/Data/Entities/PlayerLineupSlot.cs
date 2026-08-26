using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// One filled slot in a Player Pick'em lineup. Carries soft canonical
    /// refs plus a render snapshot (the pick-table pattern: the API never
    /// joins Producer's database to draw a lineup).
    ///
    /// LOCKING IS DERIVED, NEVER STORED: a slot is locked iff
    /// IsStartLocked(ContestStartUtc) — the same kickoff-minus-5-minutes
    /// rule team picks use. Derived locking self-heals schedule moves and
    /// keeps the future commissioner weekly-lock option a pure read-side
    /// change. Because locked slots reject writes, the persisted state IS
    /// the locked state — scoring reads this table as-is.
    /// </summary>
    public class PlayerLineupSlot : CanonicalEntityBase<Guid>
    {
        public Guid PlayerLineupId { get; set; }

        public PlayerLineup Lineup { get; set; } = null!;

        /// <summary>'QB','RB1','RB2','WR1','WR2','TE','FLEX','K' — the fixed v1 shape ('DEF' reserved).</summary>
        public string SlotId { get; set; } = null!;

        /// <summary>Canonical Athlete id — stable across seasons (the stats-audit lesson: never trust a roster-row's year).</summary>
        public Guid AthleteId { get; set; }

        /// <summary>The AthleteSeason used at save time; the scoring join to AthleteCompetitionStatistic.</summary>
        public Guid AthleteSeasonId { get; set; }

        /// <summary>'QB'/'RB'/'WR'/'TE'/'K' — FLEX-eligibility validation and the position badge.</summary>
        public string Position { get; set; } = null!;

        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;

        public string TeamName { get; set; } = null!;

        public string TeamSlug { get; set; } = null!;

        /// <summary>The athlete's contest for this lineup's week. Null = bye at save time (never locks, never scores, badged in the UI).</summary>
        public Guid? ContestId { get; set; }

        /// <summary>
        /// Denormalized kickoff; the lock derives from it. Same staleness
        /// contract as PickemGroupMatchup.StartDateUtc — the
        /// ContestStartTimeUpdated consumer family is the eventual updater.
        /// </summary>
        public DateTime? ContestStartUtc { get; set; }

        public string? OpponentName { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<PlayerLineupSlot>
        {
            public void Configure(EntityTypeBuilder<PlayerLineupSlot> builder)
            {
                builder.ToTable(nameof(PlayerLineupSlot));
                builder.HasKey(x => x.Id);

                builder.HasIndex(x => new { x.PlayerLineupId, x.SlotId })
                    .IsUnique();

                // The no-duplicate-athlete rule the handler pre-checks,
                // enforced at the database so concurrent saves into
                // different slots cannot slip the same athlete in twice.
                builder.HasIndex(x => new { x.PlayerLineupId, x.AthleteId })
                    .IsUnique();

                builder.Property(x => x.SlotId).HasMaxLength(8);
                builder.Property(x => x.Position).HasMaxLength(4);
                builder.Property(x => x.FirstName).HasMaxLength(100);
                builder.Property(x => x.LastName).HasMaxLength(100);
                builder.Property(x => x.TeamName).HasMaxLength(150);
                builder.Property(x => x.TeamSlug).HasMaxLength(150);
                builder.Property(x => x.OpponentName).HasMaxLength(150);
            }
        }
    }
}
