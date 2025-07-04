using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeasonRecordAts : CanonicalEntityBase<Guid>
    {
        public Guid FranchiseSeasonId { get; set; }

        public FranchiseSeason FranchiseSeason { get; set; } = default!;

        public int CategoryId { get; set; }

        public FranchiseSeasonRecordAtsCategory Category { get; set; } = default!;

        public int? Wins { get; set; }

        public int? Losses { get; set; }

        public int? Pushes { get; set; }

        public class FranchiseSeasonRecordAtsConfiguration : IEntityTypeConfiguration<FranchiseSeasonRecordAts>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonRecordAts> builder)
            {
                builder.ToTable("FranchiseSeasonRecordAts");

                builder.HasKey(e => e.Id);

                // Foreign key to FranchiseSeason
                builder.HasOne(e => e.FranchiseSeason)
                    .WithMany(season => season.RecordsAts)
                    .HasForeignKey(e => e.FranchiseSeasonId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);

                // Foreign key to AtsCategory
                builder.HasOne(e => e.Category)
                    .WithMany()
                    .HasForeignKey(e => e.CategoryId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Restrict);

                builder.Property(e => e.Wins);
                builder.Property(e => e.Losses);
                builder.Property(e => e.Pushes);
            }
        }
    }
}