using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class AthleteCareerStatistic : CanonicalEntityBase<Guid>
{
    public Guid AthleteId { get; set; }

    public string SplitId { get; set; } = string.Empty;

    public string SplitName { get; set; } = string.Empty;

    public string SplitAbbreviation { get; set; } = string.Empty;

    public AthleteBase Athlete { get; set; } = null!;

    public List<AthleteCareerStatisticCategory> Categories { get; set; } = [];

    public class EntityConfiguration : IEntityTypeConfiguration<AthleteCareerStatistic>
    {
        public void Configure(EntityTypeBuilder<AthleteCareerStatistic> builder)
        {
            builder.ToTable(nameof(AthleteCareerStatistic));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.SplitId).IsRequired().HasMaxLength(32);
            builder.Property(x => x.SplitName).IsRequired().HasMaxLength(64);
            builder.Property(x => x.SplitAbbreviation).IsRequired().HasMaxLength(32);

            builder.HasOne(x => x.Athlete)
                .WithMany()
                .HasForeignKey(x => x.AthleteId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Categories)
                .WithOne(x => x.Statistic)
                .HasForeignKey(x => x.AthleteCareerStatisticId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
