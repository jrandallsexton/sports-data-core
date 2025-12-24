using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class AthleteCompetitionStatisticCategory : CanonicalEntityBase<Guid>
{
    public Guid AthleteCompetitionStatisticId { get; set; }
    public AthleteCompetitionStatistic AthleteCompetitionStatistic { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ShortDisplayName { get; set; } = string.Empty;

    public string Abbreviation { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public List<AthleteCompetitionStatisticStat> Stats { get; set; } = [];

    public class EntityConfiguration : IEntityTypeConfiguration<AthleteCompetitionStatisticCategory>
    {
        public void Configure(EntityTypeBuilder<AthleteCompetitionStatisticCategory> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
            builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(256);
            builder.Property(x => x.ShortDisplayName).IsRequired().HasMaxLength(256);
            builder.Property(x => x.Abbreviation).IsRequired().HasMaxLength(64);
            builder.Property(x => x.Summary).HasMaxLength(1024);

            // Relationship to AthleteCompetitionStatistic is configured on the other side
            // (see AthleteCompetitionStatistic.EntityConfiguration)

            builder.HasMany(x => x.Stats)
                .WithOne(x => x.AthleteCompetitionStatisticCategory)
                .HasForeignKey(x => x.AthleteCompetitionStatisticCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}