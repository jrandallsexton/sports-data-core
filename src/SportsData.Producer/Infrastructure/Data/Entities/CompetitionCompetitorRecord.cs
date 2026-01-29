using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class CompetitionCompetitorRecord : CanonicalEntityBase<Guid>
{
    public required Guid CompetitionCompetitorId { get; set; }

    public CompetitionCompetitor CompetitionCompetitor { get; set; } = null!;

    public required string Type { get; set; }

    public string? Name { get; set; }

    public string? Summary { get; set; }

    public string? DisplayValue { get; set; }

    public double? Value { get; set; }

    public ICollection<CompetitionCompetitorRecordStat> Stats { get; set; } = [];

    public class EntityConfiguration : IEntityTypeConfiguration<CompetitionCompetitorRecord>
    {
        public void Configure(EntityTypeBuilder<CompetitionCompetitorRecord> builder)
        {
            builder.ToTable(nameof(CompetitionCompetitorRecord));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.CompetitionCompetitorId).IsRequired();
            builder.Property(x => x.Type).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Name).HasMaxLength(100);
            builder.Property(x => x.Summary).HasMaxLength(50);
            builder.Property(x => x.DisplayValue).HasMaxLength(50);
            builder.Property(x => x.Value);

            // FK: CompetitionCompetitor (parent) -> Records (children)
            builder.HasOne(x => x.CompetitionCompetitor)
                .WithMany(x => x.Records)
                .HasForeignKey(x => x.CompetitionCompetitorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Children: Stats
            builder.HasMany(x => x.Stats)
                .WithOne(x => x.CompetitionCompetitorRecord)
                .HasForeignKey(x => x.CompetitionCompetitorRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(x => x.CompetitionCompetitorId);

            // Uniqueness: one record per type per competitor
            builder.HasIndex(x => new { x.CompetitionCompetitorId, x.Type })
                .IsUnique();
        }
    }
}
