using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities;

// MLB "season series" — the season-long head-to-head between two
// teams. ESPN ships this inline alongside the current series but
// gives it no $ref / id of its own. We synthesize a deterministic
// identity from (SeasonYear, sorted FranchiseSeasonId pair) via
// DeterministicGuid.Combine so concurrent processing of multiple
// competitions between the same two teams converges on the same row.
//
// FranchiseSeasonALowId / BHighId are sorted by Guid value so the
// pair is canonical regardless of which team is home/away on any
// given competition.
//
// See docs/mlb-series-ingestion-plan.md.
public class SeasonSeries : CanonicalEntityBase<Guid>
{
    public int SeasonYear { get; set; }

    public Guid FranchiseSeasonALowId { get; set; }
    public FranchiseSeason FranchiseSeasonALow { get; set; } = default!;

    public Guid FranchiseSeasonBHighId { get; set; }
    public FranchiseSeason FranchiseSeasonBHigh { get; set; } = default!;

    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Summary { get; set; }

    public bool Completed { get; set; }
    public int TotalCompetitions { get; set; }

    public DateTimeOffset? StartDate { get; set; }

    public ICollection<SeasonSeriesCompetitor> Competitors { get; set; } = [];
    public ICollection<BaseballCompetition> Competitions { get; set; } = [];

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonSeries>
    {
        public void Configure(EntityTypeBuilder<SeasonSeries> builder)
        {
            builder.ToTable(nameof(SeasonSeries));
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => new { x.SeasonYear, x.FranchiseSeasonALowId, x.FranchiseSeasonBHighId })
                .IsUnique();

            builder.Property(x => x.Title).HasMaxLength(100);
            builder.Property(x => x.Description).HasMaxLength(250);
            builder.Property(x => x.Summary).HasMaxLength(250);

            builder.HasOne(x => x.FranchiseSeasonALow)
                .WithMany()
                .HasForeignKey(x => x.FranchiseSeasonALowId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.FranchiseSeasonBHigh)
                .WithMany()
                .HasForeignKey(x => x.FranchiseSeasonBHighId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.Competitors)
                .WithOne(x => x.SeasonSeries)
                .HasForeignKey(x => x.SeasonSeriesId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
