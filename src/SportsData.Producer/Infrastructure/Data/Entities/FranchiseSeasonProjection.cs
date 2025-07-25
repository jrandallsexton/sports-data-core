using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;
using System;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeasonProjection : CanonicalEntityBase<Guid>
    {
        public Guid FranchiseSeasonId { get; set; } // FK to FranchiseSeason

        public FranchiseSeason Season { get; set; } = null!;

        public Guid FranchiseId { get; set; } // denormalized for convenience

        public int SeasonYear { get; set; } // denormalized for convenience

        public decimal ChanceToWinDivision { get; set; }

        public decimal ChanceToWinConference { get; set; }

        public decimal ProjectedWins { get; set; }

        public decimal ProjectedLosses { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonProjection>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonProjection> builder)
            {
                builder.ToTable(nameof(FranchiseSeasonProjection));
                builder.HasKey(t => t.Id);

                builder.Property(t => t.ChanceToWinDivision).IsRequired();
                builder.Property(t => t.ChanceToWinConference).IsRequired();
                builder.Property(t => t.ProjectedWins).IsRequired();
                builder.Property(t => t.ProjectedLosses).IsRequired();
                builder.Property(t => t.SeasonYear).IsRequired();
                builder.Property(t => t.FranchiseId).IsRequired();

                builder.HasOne(t => t.Season)
                    .WithMany()
                    .HasForeignKey(t => t.FranchiseSeasonId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
