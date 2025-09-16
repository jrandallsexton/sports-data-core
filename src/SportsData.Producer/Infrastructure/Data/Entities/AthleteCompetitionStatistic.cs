using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class AthleteCompetitionStatistic : CanonicalEntityBase<Guid>
    {
        public Guid AthleteSeasonId { get; set; }

        public AthleteSeason AthleteSeason { get; set; } = null!;

        public Guid CompetitionId { get; set; }

        public Competition Competition { get; set; } = null!;

        public ICollection<AthleteCompetitionStatisticCategory> Categories { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<AthleteCompetitionStatistic>
        {
            public void Configure(EntityTypeBuilder<AthleteCompetitionStatistic> builder)
            {
                builder.HasKey(x => x.Id);

                builder.HasIndex(x => new { x.AthleteSeasonId, x.CompetitionId }).IsUnique();

                builder.HasOne(x => x.AthleteSeason)
                    .WithMany()
                    .HasForeignKey(x => x.AthleteSeasonId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.Competition)
                    .WithMany()
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder.HasMany(x => x.Categories)
                    .WithOne(x => x.AthleteCompetitionStatistic)
                    .HasForeignKey(x => x.AthleteCompetitionStatisticId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }

    }
}
