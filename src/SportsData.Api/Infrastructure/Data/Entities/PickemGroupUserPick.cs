﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Api.Application;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class PickemGroupUserPick : CanonicalEntityBase<Guid>
    {
        public Guid PickemGroupId { get; set; }

        public Guid UserId { get; set; }

        public User User { get; set; } = null!; // Navigation property to User

        public Guid ContestId { get; set; } // From Producer

        public int Week { get; set; }

        //public Contest Contest { get; set; } = null!;

        public Guid? FranchiseId { get; set; } // Used for SU or ATS

        public OverUnderPick? OverUnder { get; set; } // Enum: Over, Under

        public int? ConfidencePoints { get; set; }

        public UserPickType PickType { get; set; } = UserPickType.StraightUp;

        // === Scoring Fields ===
        public bool? IsCorrect { get; set; }

        public int? PointsAwarded { get; set; }

        public bool? WasAgainstSpread { get; set; }

        public DateTime? ScoredAt { get; set; }

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
            }
        }
    }
}