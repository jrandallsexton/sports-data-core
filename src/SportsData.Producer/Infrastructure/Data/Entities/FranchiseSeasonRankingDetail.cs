using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class FranchiseSeasonRankingDetail : CanonicalEntityBase<Guid>
{
    public Guid FranchiseSeasonRankingId { get; set; }

    public FranchiseSeasonRanking FranchiseSeasonRanking { get; set; } = null!;

    public int Current { get; set; }

    public int Previous { get; set; }

    public double Points { get; set; }

    public int FirstPlaceVotes { get; set; }

    public string Trend { get; set; } = string.Empty;

    public DateTime? Date { get; set; }

    public DateTime? LastUpdated { get; set; }

    public FranchiseSeasonRankingDetailRecord? Record { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonRankingDetail>
    {
        public void Configure(EntityTypeBuilder<FranchiseSeasonRankingDetail> builder)
        {
            builder.ToTable(nameof(FranchiseSeasonRankingDetail));

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Current)
                .IsRequired();

            builder.Property(x => x.Previous)
                .IsRequired();

            builder.Property(x => x.Points)
                .IsRequired();

            builder.Property(x => x.FirstPlaceVotes)
                .IsRequired();

            builder.Property(x => x.Trend)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(x => x.Date)
                .IsRequired()
                .HasMaxLength(40);

            builder.Property(x => x.LastUpdated)
                .IsRequired()
                .HasMaxLength(40);

            builder.HasOne(x => x.FranchiseSeasonRanking)
                .WithOne(x => x.Rank)
                .HasForeignKey<FranchiseSeasonRankingDetail>(x => x.FranchiseSeasonRankingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.OwnsOne(x => x.Record, record =>
            {
                record.Property(r => r.Summary)
                      .HasColumnName("RecordSummary")
                      .HasMaxLength(20);

                record.Navigation(r => r.Stats).HasField("_stats"); // optional, if backing field used
            });
        }
    }
}
