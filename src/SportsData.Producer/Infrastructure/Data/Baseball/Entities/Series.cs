using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities;

// MLB "current series" — the active multi-game set between two teams.
// Identity is a deterministic Guid derived from the ESPN seriesId so
// concurrent processing of multiple competitions in the same series
// converges on the same row. EspnSeriesId is preserved as a column
// for traceability; no separate *ExternalId table because there's
// only ever one provider for this data today.
//
// See docs/mlb-series-ingestion-plan.md.
public class Series : CanonicalEntityBase<Guid>
{
    public string EspnSeriesId { get; set; } = default!;

    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Summary { get; set; }

    public bool Completed { get; set; }
    public int TotalCompetitions { get; set; }

    public DateTimeOffset? StartDate { get; set; }

    public ICollection<SeriesCompetitor> Competitors { get; set; } = [];
    public ICollection<BaseballCompetition> Competitions { get; set; } = [];

    public class EntityConfiguration : IEntityTypeConfiguration<Series>
    {
        public void Configure(EntityTypeBuilder<Series> builder)
        {
            builder.ToTable(nameof(Series));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.EspnSeriesId).HasMaxLength(50).IsRequired();
            builder.HasIndex(x => x.EspnSeriesId).IsUnique();

            builder.Property(x => x.Title).HasMaxLength(100);
            builder.Property(x => x.Description).HasMaxLength(250);
            builder.Property(x => x.Summary).HasMaxLength(250);

            builder.HasMany(x => x.Competitors)
                .WithOne(x => x.Series)
                .HasForeignKey(x => x.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);

            // BaseballCompetition.CurrentSeriesId nullable FK is configured
            // on the BaseballCompetition side.
        }
    }
}
