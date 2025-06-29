using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeasonStatisticCategory : CanonicalEntityBase<Guid>
    {
        public required Guid FranchiseSeasonId { get; set; }

        public FranchiseSeason FranchiseSeason { get; set; } = null!;

        public required string Name { get; set; }

        public required string DisplayName { get; set; }

        public required string ShortDisplayName { get; set; }

        public required string Abbreviation { get; set; }

        public string? Summary { get; set; }

        public ICollection<FranchiseSeasonStatistic> Stats { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonStatisticCategory>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonStatisticCategory> builder)
            {
                builder.ToTable("FranchiseSeasonStatisticCategory");
                builder.HasKey(e => e.Id);

                // FK to FranchiseSeason
                builder.HasOne(e => e.FranchiseSeason)
                    .WithMany(f => f.Statistics)
                    .HasForeignKey(e => e.FranchiseSeasonId)
                    .OnDelete(DeleteBehavior.Cascade);

                // 1:N to Stats
                builder.HasMany(e => e.Stats)
                    .WithOne(s => s.Category)
                    .HasForeignKey(s => s.FranchiseSeasonStatisticCategoryId)
                    .OnDelete(DeleteBehavior.Cascade);

                // String properties
                builder.Property(e => e.Name).IsRequired().HasMaxLength(256);
                builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(256);
                builder.Property(e => e.ShortDisplayName).IsRequired().HasMaxLength(256);
                builder.Property(e => e.Abbreviation).IsRequired().HasMaxLength(64);
                builder.Property(e => e.Summary).HasMaxLength(512);
            }
        }
    }

}
