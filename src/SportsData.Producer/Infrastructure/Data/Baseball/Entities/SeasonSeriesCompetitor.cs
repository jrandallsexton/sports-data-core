using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities;

public class SeasonSeriesCompetitor : CanonicalEntityBase<Guid>
{
    public Guid SeasonSeriesId { get; set; }
    public SeasonSeries SeasonSeries { get; set; } = default!;

    public Guid FranchiseSeasonId { get; set; }
    public FranchiseSeason FranchiseSeason { get; set; } = default!;

    public int Wins { get; set; }
    public int Ties { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonSeriesCompetitor>
    {
        public void Configure(EntityTypeBuilder<SeasonSeriesCompetitor> builder)
        {
            builder.ToTable(nameof(SeasonSeriesCompetitor));
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => new { x.SeasonSeriesId, x.FranchiseSeasonId }).IsUnique();

            builder.HasOne(x => x.FranchiseSeason)
                .WithMany()
                .HasForeignKey(x => x.FranchiseSeasonId)
                .OnDelete(DeleteBehavior.Restrict);

            // SeasonSeries → Competitors relationship configured on SeasonSeries side.
        }
    }
}
