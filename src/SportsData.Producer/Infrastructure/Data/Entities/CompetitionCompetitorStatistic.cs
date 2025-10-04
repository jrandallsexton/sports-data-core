using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionCompetitorStatistic : CanonicalEntityBase<Guid>
    {
        public Guid? CompetitionCompetitorId { get; set; }
        public CompetitionCompetitor? CompetitionCompetitor { get; set; }

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

                // 🔗 FKs / navs
                builder.HasOne(x => x.CompetitionCompetitor)
                    .WithMany(c => c.Statistics)
                    .HasForeignKey(x => x.CompetitionCompetitorId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.FranchiseSeason)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseSeasonId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder.HasOne(x => x.Competition)
                    .WithMany()
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder.HasMany(x => x.Categories)
                    .WithOne(x => x.CompetitionCompetitorStatistic)
                    .HasForeignKey(x => x.CompetitionCompetitorStatisticId)
                    .OnDelete(DeleteBehavior.Cascade);

                // 🧭 Indexes
                builder.HasIndex(x => x.CompetitionCompetitorId);
                builder.HasIndex(x => x.CompetitionId);
                builder.HasIndex(x => x.FranchiseSeasonId);

                // ✅ One stats record per team per competition
                builder.HasIndex(x => new { x.FranchiseSeasonId, x.CompetitionId })
                    .IsUnique();
            }
        }

    }
}
