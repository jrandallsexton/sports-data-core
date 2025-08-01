﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionLeaderStat : CanonicalEntityBase<Guid>
    {
        public Guid CompetitionLeaderId { get; set; }
        public CompetitionLeader CompetitionLeader { get; set; } = null!;

        public string DisplayValue { get; set; } = null!;
        public double Value { get; set; }

        public Guid AthleteId { get; set; }
        public Athlete Athlete { get; set; } = null!;

        public Guid FranchiseSeasonId { get; set; }
        public FranchiseSeason FranchiseSeason { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionLeaderStat>
        {
            public void Configure(EntityTypeBuilder<CompetitionLeaderStat> builder)
            {
                builder.ToTable(nameof(CompetitionLeaderStat));

                builder.HasKey(x => x.Id);

                builder.Property(x => x.DisplayValue)
                    .HasMaxLength(64);

                builder.Property(x => x.Value)
                    .HasPrecision(18, 6);

                builder.HasOne(x => x.CompetitionLeader)
                    .WithMany(x => x.Stats)
                    .HasForeignKey(x => x.CompetitionLeaderId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.Athlete)
                    .WithMany()
                    .HasForeignKey(x => x.AthleteId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder.HasOne(x => x.FranchiseSeason)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseSeasonId)
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }
    }
}