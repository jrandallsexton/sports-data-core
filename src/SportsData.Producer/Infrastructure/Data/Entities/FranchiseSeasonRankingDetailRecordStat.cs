using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class FranchiseSeasonRankingDetailRecordStat : CanonicalEntityBase<Guid>
{
    public Guid FranchiseSeasonRankingDetailRecordId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ShortDisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Abbreviation { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public double Value { get; set; }

    public string DisplayValue { get; set; } = string.Empty;

    public FranchiseSeasonRankingDetailRecord? FranchiseSeasonRankingDetailRecord { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonRankingDetailRecordStat>
    {
        public void Configure(EntityTypeBuilder<FranchiseSeasonRankingDetailRecordStat> builder)
        {
            builder.ToTable(nameof(FranchiseSeasonRankingDetailRecordStat));

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.ShortDisplayName)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.Description)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.Abbreviation)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(x => x.Type)
                .IsRequired()
                .HasMaxLength(30);

            builder.Property(x => x.Value)
                .IsRequired();

            builder.Property(x => x.DisplayValue)
                .IsRequired()
                .HasMaxLength(20);

            builder.HasOne(x => x.FranchiseSeasonRankingDetailRecord)
                .WithMany(x => x.Stats)
                .HasForeignKey(x => x.FranchiseSeasonRankingDetailRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
