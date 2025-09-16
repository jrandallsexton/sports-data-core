using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class CompetitionCompetitorStatisticStat : CanonicalEntityBase<Guid>
{
    public Guid CompetitionCompetitorStatisticCategoryId { get; set; }
    public CompetitionCompetitorStatisticCategory CompetitionCompetitorStatisticCategory { get; set; } = null!;

    public required string Name { get; set; }

    public required string DisplayName { get; set; }

    public required string ShortDisplayName { get; set; }

    public string? Description { get; set; }

    public required string Abbreviation { get; set; } = string.Empty;

    public decimal? Value { get; set; }

    public string DisplayValue { get; set; } = string.Empty;

    public class EntityConfiguration : IEntityTypeConfiguration<CompetitionCompetitorStatisticStat>
    {
        public void Configure(EntityTypeBuilder<CompetitionCompetitorStatisticStat> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
            builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(256);
            builder.Property(x => x.ShortDisplayName).IsRequired().HasMaxLength(256);
            builder.Property(x => x.Abbreviation).IsRequired().HasMaxLength(64);
            builder.Property(x => x.Description).HasMaxLength(1024);
            builder.Property(x => x.DisplayValue).HasMaxLength(256);
            builder.Property(x => x.Value).HasPrecision(18, 6);

            builder.HasOne(x => x.CompetitionCompetitorStatisticCategory)
                .WithMany(x => x.Stats)
                .HasForeignKey(x => x.CompetitionCompetitorStatisticCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}