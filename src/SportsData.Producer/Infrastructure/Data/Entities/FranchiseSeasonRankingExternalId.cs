using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class FranchiseSeasonRankingExternalId : ExternalId
{
    public Guid RankingId { get; set; }

    public FranchiseSeasonRanking Ranking { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonRankingExternalId>
    {
        public void Configure(EntityTypeBuilder<FranchiseSeasonRankingExternalId> builder)
        {
            builder.ToTable(nameof(FranchiseSeasonRankingExternalId));

            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.Ranking)
                .WithMany(r => r.ExternalIds)
                .HasForeignKey(x => x.RankingId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}