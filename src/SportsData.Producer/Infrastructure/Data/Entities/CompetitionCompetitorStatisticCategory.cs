using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class CompetitionCompetitorStatisticCategory : CanonicalEntityBase<Guid>
{
    public Guid CompetitionCompetitorStatisticId { get; set; }
    public CompetitionCompetitorStatistic CompetitionCompetitorStatistic { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ShortDisplayName { get; set; } = string.Empty;

    public string Abbreviation { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public List<CompetitionCompetitorStatisticStat> Stats { get; set; } = [];

    public class EntityConfiguration : IEntityTypeConfiguration<CompetitionCompetitorStatisticCategory>
    {
        public void Configure(EntityTypeBuilder<CompetitionCompetitorStatisticCategory> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
            builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(256);
            builder.Property(x => x.ShortDisplayName).IsRequired().HasMaxLength(256);
            builder.Property(x => x.Abbreviation).IsRequired().HasMaxLength(64);
            builder.Property(x => x.Summary).HasMaxLength(1024);

            builder.HasMany(x => x.Stats)
                .WithOne(x => x.CompetitionCompetitorStatisticCategory)
                .HasForeignKey(x => x.CompetitionCompetitorStatisticCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}