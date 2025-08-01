using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class FranchiseSeasonRankingOccurrence : CanonicalEntityBase<Guid>
{
    public Guid RankingId { get; set; }

    public FranchiseSeasonRanking Ranking { get; set; } = null!;

    public int Number { get; set; }

    public required string Type { get; set; }

    public bool Last { get; set; }

    public string Value { get; set; } = string.Empty;

    public string DisplayValue { get; set; } = string.Empty;

    public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonRankingOccurrence>
    {
        public void Configure(EntityTypeBuilder<FranchiseSeasonRankingOccurrence> builder)
        {
            builder.ToTable(nameof(FranchiseSeasonRankingOccurrence));

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Type)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.Value)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.DisplayValue)
                .IsRequired()
                .HasMaxLength(30);

            builder.Property(x => x.Last).IsRequired();

            builder.HasOne(x => x.Ranking)
                .WithOne(x => x.Occurrence)
                .HasForeignKey<FranchiseSeasonRankingOccurrence>(x => x.RankingId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}