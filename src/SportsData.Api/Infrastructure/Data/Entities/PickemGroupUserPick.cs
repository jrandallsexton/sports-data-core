using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Api.Application.Common.Enums;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class PickemGroupUserPick : CanonicalEntityBase<Guid>
    {
        public Guid PickemGroupId { get; set; }

        public PickemGroup Group { get; set; } = null!;

        public Guid UserId { get; set; }

        public User User { get; set; } = null!; // Navigation property to User

        public Guid ContestId { get; set; } // From Producer

        public int Week { get; set; }

        // The picked team, stored as a FranchiseSeasonId (matches Contest's
        // Home/AwayTeamFranchiseSeasonId). Set for SU/ATS; null for Over/Under.
        public Guid? FranchiseSeasonId { get; set; }

        public OverUnderPick? OverUnder { get; set; } // Enum: Over, Under

        public int? ConfidencePoints { get; set; }

        public PickType PickType { get; set; } = PickType.StraightUp;

        public string? SyntheticPickStyle { get; set; }

        // === Scoring Fields ===
        public bool? IsCorrect { get; set; }

        public int? PointsAwarded { get; set; }

        public bool? WasAgainstSpread { get; set; }

        public DateTime? ScoredAt { get; set; }

        /// <summary>
        /// When the nightly audit last re-verified this pick against canonical
        /// data. Null means "needs auditing" — that is the ONLY thing the
        /// audit job selects on, so this is a watermark rather than a log.
        ///
        /// Cleared whenever the pick's inputs could have moved: a score
        /// correction (ContestScoreChanged), a re-enrichment
        /// (ContestFinalized — which is how a spread-winner or over/under fix
        /// arrives WITHOUT the score changing), or a re-score. Without the
        /// watermark the job re-audited every pick ever scored on every run:
        /// 1,284 contests on 2026-08-24, almost all of them settled 2025
        /// games, growing every week forever.
        /// </summary>
        public DateTime? AuditedUtc { get; set; }

        // === Tiebreaker Fields ===
        public TiebreakerType TiebreakerType { get; set; }

        public int? TiebreakerGuessTotal { get; set; }

        public int? TiebreakerGuessHome { get; set; }

        public int? TiebreakerGuessAway { get; set; }

        public int? TiebreakerActualTotal { get; set; }

        public int? TiebreakerActualHome { get; set; }

        public int? TiebreakerActualAway { get; set; }

        // Optional traceability (e.g., imported from another league)
        public Guid? ImportedFromPickId { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<PickemGroupUserPick>
        {
            public void Configure(EntityTypeBuilder<PickemGroupUserPick> builder)
            {
                builder.ToTable("UserPick");
                builder.HasKey(x => x.Id);

                // Correct composite unique index: one pick per user, per group, per contest
                builder.HasIndex(x => new { PickemGroupId = x.PickemGroupId, x.UserId, x.ContestId }).IsUnique();

                // Optional performance index (non-unique) if querying by contest
                builder.HasIndex(c => c.ContestId);

                builder.Property(x => x.TiebreakerType)
                    .HasConversion<int>() // store as int
                    .IsRequired();

                builder.Property(x => x.PickType)
                    .HasConversion<int>()
                    .IsRequired();

                builder.HasOne(x => x.Group)
                    .WithMany() // or `.WithMany(g => g.UserPicks)` if collection exists
                    .HasForeignKey(x => x.PickemGroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.User)
                    .WithMany() // or `.WithMany(u => u.UserPicks)` if applicable
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.Property(u => u.SyntheticPickStyle)
                    .HasMaxLength(100);
            }
        }
    }
}