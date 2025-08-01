using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class FranchiseSeasonRankingDetailRecord : CanonicalEntityBase<Guid>
{
    public Guid FranchiseSeasonRankingDetailId { get; set; }

    public FranchiseSeasonRankingDetail? FranchiseSeasonRankingDetail { get; set; }

    public string Summary { get; set; } = string.Empty;

    public ICollection<FranchiseSeasonRankingDetailRecordStat> Stats { get; set; } = [];

    public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonRankingDetailRecord>
    {
        public void Configure(EntityTypeBuilder<FranchiseSeasonRankingDetailRecord> builder)
        {
            builder.ToTable(nameof(FranchiseSeasonRankingDetailRecord));

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Summary)
                .IsRequired()
                .HasMaxLength(20); // Adjust if you expect longer record strings

            builder.HasOne(x => x.FranchiseSeasonRankingDetail)
                .WithOne(x => x.Record)
                .HasForeignKey<FranchiseSeasonRankingDetailRecord>(x => x.FranchiseSeasonRankingDetailId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Stats)
                .WithOne(x => x.FranchiseSeasonRankingDetailRecord)
                .HasForeignKey(x => x.FranchiseSeasonRankingDetailRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}