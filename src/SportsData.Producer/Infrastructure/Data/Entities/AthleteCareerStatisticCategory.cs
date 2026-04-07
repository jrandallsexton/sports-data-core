using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class AthleteCareerStatisticCategory : CanonicalEntityBase<Guid>
{
    public Guid AthleteCareerStatisticId { get; set; }

    public AthleteCareerStatistic Statistic { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ShortDisplayName { get; set; } = string.Empty;

    public string Abbreviation { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public List<AthleteCareerStatisticStat> Stats { get; set; } = [];

    public class EntityConfiguration : IEntityTypeConfiguration<AthleteCareerStatisticCategory>
    {
        public void Configure(EntityTypeBuilder<AthleteCareerStatisticCategory> builder)
        {
            builder.ToTable(nameof(AthleteCareerStatisticCategory));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).IsRequired().HasMaxLength(64);
            builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(64);
            builder.Property(x => x.ShortDisplayName).IsRequired().HasMaxLength(32);
            builder.Property(x => x.Abbreviation).IsRequired().HasMaxLength(32);
            builder.Property(x => x.Summary).HasMaxLength(256);

            builder.HasOne(x => x.Statistic)
                .WithMany(x => x.Categories)
                .HasForeignKey(x => x.AthleteCareerStatisticId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Stats)
                .WithOne(x => x.Category)
                .HasForeignKey(x => x.AthleteCareerStatisticCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
