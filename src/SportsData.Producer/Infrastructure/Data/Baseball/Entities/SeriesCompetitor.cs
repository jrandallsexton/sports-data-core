using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities;

// Per-team participation in a Series. Wins/Ties carry the running
// counters from ESPN's series.competitors[] array.
public class SeriesCompetitor : CanonicalEntityBase<Guid>
{
    public Guid SeriesId { get; set; }
    public Series Series { get; set; } = default!;

    public Guid FranchiseSeasonId { get; set; }
    public FranchiseSeason FranchiseSeason { get; set; } = default!;

    public int Wins { get; set; }
    public int Ties { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<SeriesCompetitor>
    {
        public void Configure(EntityTypeBuilder<SeriesCompetitor> builder)
        {
            builder.ToTable(nameof(SeriesCompetitor));
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => new { x.SeriesId, x.FranchiseSeasonId }).IsUnique();

            builder.HasOne(x => x.FranchiseSeason)
                .WithMany()
                .HasForeignKey(x => x.FranchiseSeasonId)
                .OnDelete(DeleteBehavior.Restrict);

            // Series → Competitors relationship configured on Series side.
        }
    }
}
