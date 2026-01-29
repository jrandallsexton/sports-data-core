using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class CompetitionCompetitorRecordStat : CanonicalEntityBase<Guid>
{
    public required Guid CompetitionCompetitorRecordId { get; set; }

    public CompetitionCompetitorRecord CompetitionCompetitorRecord { get; set; } = null!;

    public required string Name { get; set; }

    public string? DisplayName { get; set; }

    public string? ShortDisplayName { get; set; }

    public string? Description { get; set; }

    public string? Abbreviation { get; set; }

    public string? Type { get; set; }

    public double? Value { get; set; }

    public string? DisplayValue { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<CompetitionCompetitorRecordStat>
    {
        public void Configure(EntityTypeBuilder<CompetitionCompetitorRecordStat> builder)
        {
            builder.ToTable(nameof(CompetitionCompetitorRecordStat));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.CompetitionCompetitorRecordId).IsRequired();
            builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
            builder.Property(x => x.DisplayName).HasMaxLength(200);
            builder.Property(x => x.ShortDisplayName).HasMaxLength(50);
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.Abbreviation).HasMaxLength(20);
            builder.Property(x => x.Type).HasMaxLength(50);
            builder.Property(x => x.Value);
            builder.Property(x => x.DisplayValue).HasMaxLength(100);

            // FK: CompetitionCompetitorRecord (parent) -> Stats (children)
            builder.HasOne(x => x.CompetitionCompetitorRecord)
                .WithMany(x => x.Stats)
                .HasForeignKey(x => x.CompetitionCompetitorRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(x => x.CompetitionCompetitorRecordId);
            builder.HasIndex(x => x.Name);
        }
    }
}
