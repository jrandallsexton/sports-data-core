using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class AthleteSeasonStatistic : CanonicalEntityBase<Guid>
{
    public Guid AthleteSeasonId { get; set; }

    public string SplitId { get; set; } = string.Empty;

    public string SplitName { get; set; } = string.Empty;

    public string SplitAbbreviation { get; set; } = string.Empty;

    public string SplitType { get; set; } = string.Empty;

    public AthleteSeason AthleteSeason { get; set; } = null!;

    public List<AthleteSeasonStatisticCategory> Categories { get; set; } = [];

    public class EntityConfiguration : IEntityTypeConfiguration<AthleteSeasonStatistic>
    {
        public void Configure(EntityTypeBuilder<AthleteSeasonStatistic> builder)
        {
            builder.ToTable(nameof(AthleteSeasonStatistic));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.SplitId).IsRequired().HasMaxLength(32);
            builder.Property(x => x.SplitName).IsRequired().HasMaxLength(64);
            builder.Property(x => x.SplitAbbreviation).IsRequired().HasMaxLength(32);
            builder.Property(x => x.SplitType).IsRequired().HasMaxLength(32);

            builder.HasOne(x => x.AthleteSeason)
                .WithMany() // consider `.WithMany(x => x.Statistics)` if you add nav property
                .HasForeignKey(x => x.AthleteSeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Categories)
                .WithOne(x => x.Statistic)
                .HasForeignKey(x => x.AthleteSeasonStatisticId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}