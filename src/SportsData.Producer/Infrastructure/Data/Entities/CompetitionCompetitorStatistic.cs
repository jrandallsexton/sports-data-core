using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionCompetitorStatistic : CanonicalEntityBase<Guid>
    {
        public Guid FranchiseSeasonId { get; set; }
        public FranchiseSeason FranchiseSeason { get; set; } = null!;

        public Guid CompetitionId { get; set; }
        public Competition Competition { get; set; } = null!;

        public ICollection<CompetitionCompetitorStatisticCategory> Categories { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionCompetitorStatistic>
        {
            public void Configure(EntityTypeBuilder<CompetitionCompetitorStatistic> builder)
            {
                builder.HasKey(x => x.Id);

                builder.HasIndex(x => new { x.FranchiseSeasonId, x.CompetitionId }).IsUnique();

                builder.HasOne(x => x.FranchiseSeason)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseSeasonId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.Competition)
                    .WithMany()
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.Categories)
                    .WithOne(x => x.CompetitionCompetitorStatistic)
                    .HasForeignKey(x => x.CompetitionCompetitorStatisticId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
