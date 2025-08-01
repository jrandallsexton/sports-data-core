using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class AthleteSeasonStatisticCategory : CanonicalEntityBase<Guid>
{
    public Guid AthleteSeasonStatisticId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ShortDisplayName { get; set; } = string.Empty;

    public string Abbreviation { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public AthleteSeasonStatistic Statistic { get; set; } = null!;

    public List<AthleteSeasonStatisticStat> Stats { get; set; } = [];

    public class EntityConfiguration : IEntityTypeConfiguration<AthleteSeasonStatisticCategory>
    {
        public void Configure(EntityTypeBuilder<AthleteSeasonStatisticCategory> builder)
        {
            builder.ToTable(nameof(AthleteSeasonStatisticCategory));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).IsRequired().HasMaxLength(64);
            builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(64);
            builder.Property(x => x.ShortDisplayName).IsRequired().HasMaxLength(32);
            builder.Property(x => x.Abbreviation).IsRequired().HasMaxLength(32);
            builder.Property(x => x.Summary).HasMaxLength(256);

            builder.HasOne(x => x.Statistic)
                .WithMany(x => x.Categories)
                .HasForeignKey(x => x.AthleteSeasonStatisticId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Stats)
                .WithOne(x => x.Category)
                .HasForeignKey(x => x.AthleteSeasonStatisticCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}