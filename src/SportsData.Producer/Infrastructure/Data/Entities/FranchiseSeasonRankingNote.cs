using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class FranchiseSeasonRankingNote : CanonicalEntityBase<Guid>
{
    public Guid RankingId { get; set; }

    public FranchiseSeasonRanking Ranking { get; set; } = null!;

    public required string Text { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonRankingNote>
    {
        public void Configure(EntityTypeBuilder<FranchiseSeasonRankingNote> builder)
        {
            builder.ToTable(nameof(FranchiseSeasonRankingNote));

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Text)
                .IsRequired()
                .HasMaxLength(512);

            builder.HasOne(x => x.Ranking)
                .WithMany(r => r.Notes)
                .HasForeignKey(x => x.RankingId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}