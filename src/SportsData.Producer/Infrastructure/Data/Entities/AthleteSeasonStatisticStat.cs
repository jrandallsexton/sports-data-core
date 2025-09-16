using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class AthleteSeasonStatisticStat : CanonicalEntityBase<Guid>
{
    public Guid AthleteSeasonStatisticCategoryId { get; set; }

    public AthleteSeasonStatisticCategory Category { get; set; } = null!;

    public required string Name { get; set; }

    public required string DisplayName { get; set; }

    public required string ShortDisplayName { get; set; }

    public string? Description { get; set; }

    public required string Abbreviation { get; set; } = string.Empty;

    public string DisplayValue { get; set; } = string.Empty;

    public string? PerGameDisplayValue { get; set; }

    public decimal? Value { get; set; }

    public decimal? PerGameValue { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<AthleteSeasonStatisticStat>
    {
        public void Configure(EntityTypeBuilder<AthleteSeasonStatisticStat> builder)
        {
            builder.ToTable(nameof(AthleteSeasonStatisticStat));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).IsRequired().HasMaxLength(64);
            builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(64);
            builder.Property(x => x.ShortDisplayName).IsRequired().HasMaxLength(32);
            builder.Property(x => x.Description).HasMaxLength(256);
            builder.Property(x => x.Abbreviation).IsRequired().HasMaxLength(32);
            builder.Property(x => x.DisplayValue).IsRequired().HasMaxLength(32);
            builder.Property(x => x.PerGameDisplayValue).HasMaxLength(32);

            builder.Property(x => x.Value).HasPrecision(18, 6);
            builder.Property(x => x.PerGameValue).HasPrecision(18, 6);

            builder.HasOne(x => x.Category)
                .WithMany(x => x.Stats)
                .HasForeignKey(x => x.AthleteSeasonStatisticCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
