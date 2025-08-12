using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class SeasonRankingExternalId : ExternalId
    {
        public Guid SeasonRankingId { get; set; }

        public SeasonRanking SeasonRanking { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<SeasonRankingExternalId>
        {
            public void Configure(EntityTypeBuilder<SeasonRankingExternalId> builder)
            {
                builder.ToTable(nameof(SeasonRankingExternalId));

                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();

                builder.Property(e => e.Value).IsRequired().HasMaxLength(256);
                builder.Property(e => e.SourceUrl).HasMaxLength(2048);
                builder.Property(e => e.SourceUrlHash).IsRequired().HasMaxLength(128);

                // One SeasonRanking -> many external IDs
                builder.HasOne(e => e.SeasonRanking)
                    .WithMany(r => r.ExternalIds)
                    .HasForeignKey(e => e.SeasonRankingId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
