﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Api.Application;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class PickemGroup : CanonicalEntityBase<Guid>
    {
        public required string Name { get; set; }

        public required Sport Sport { get; set; }

        public required League League { get; set; }

        public PickType PickType { get; set; } = PickType.StraightUp;

        public TiebreakerType TiebreakerType { get; set; } = TiebreakerType.None;

        public TiebreakerTiePolicy TiebreakerTiePolicy { get; set; } = TiebreakerTiePolicy.EarliestSubmission;

        public bool UseConfidencePoints { get; set; }

        public bool IsPublic { get; set; }

        public Guid CommissionerUserId { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<PickemGroup>
        {
            public void Configure(EntityTypeBuilder<PickemGroup> builder)
            {
                builder.ToTable(nameof(PickemGroup));
                builder.HasKey(x => x.Id);
                builder.HasIndex(x => x.CommissionerUserId);
                builder.Property(x => x.Name).HasMaxLength(100);
                builder.Property(l => l.PickType)
                    .HasConversion<int>() // Store bitflag as int
                    .IsRequired();
                builder.Property(x => x.TiebreakerType)
                    .HasConversion<int>() // store as int
                    .IsRequired();
                builder.Property(x => x.TiebreakerTiePolicy)
                    .HasConversion<int>() // store as int
                    .IsRequired();

            }
        }
    }

}
