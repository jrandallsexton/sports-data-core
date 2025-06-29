using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeasonStatistic : CanonicalEntityBase<Guid>
    {
        public Guid FranchiseSeasonStatisticCategoryId { get; set; }

        public FranchiseSeasonStatisticCategory Category { get; set; } = null!;

        public required string Name { get; set; }

        public required string DisplayName { get; set; }

        public required string ShortDisplayName { get; set; }

        public required string Description { get; set; }

        public required string Abbreviation { get; set; }

        public decimal Value { get; set; }

        public required string DisplayValue { get; set; }

        public int Rank { get; set; }

        public string? RankDisplayValue { get; set; }

        public decimal? PerGameValue { get; set; }

        public string? PerGameDisplayValue { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonStatistic>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonStatistic> builder)
            {
                builder.ToTable("FranchiseSeasonStatistic");
                builder.HasKey(e => e.Id);

                // Define the required FK to Category
                builder.HasOne(e => e.Category)
                    .WithMany(c => c.Stats)
                    .HasForeignKey(e => e.FranchiseSeasonStatisticCategoryId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Property configurations
                builder.Property(e => e.Name).IsRequired().HasMaxLength(256);
                builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(256);
                builder.Property(e => e.ShortDisplayName).IsRequired().HasMaxLength(256);
                builder.Property(e => e.Description).IsRequired().HasMaxLength(512);
                builder.Property(e => e.Abbreviation).IsRequired().HasMaxLength(64);
                builder.Property(e => e.DisplayValue).IsRequired().HasMaxLength(64);
                builder.Property(e => e.RankDisplayValue).HasMaxLength(64);
                builder.Property(e => e.PerGameDisplayValue).HasMaxLength(64);

                // Precision for decimals
                builder.Property(e => e.Value).HasPrecision(18, 6);
                builder.Property(e => e.PerGameValue).HasPrecision(18, 6);
            }
        }
    }

}
