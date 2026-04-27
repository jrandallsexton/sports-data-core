using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities;

// Child of BaseballCompetitionStatus. ESPN's MLB status payload
// carries a featuredAthletes[] array (e.g. winningPitcher /
// losingPitcher post-game; current batter / pitcher in-game) and
// each entry references an athlete, team, and statistics endpoint.
// Lives in Baseball/Entities/ — the football side has no analogue
// and no table for this.
public class BaseballCompetitionStatusFeaturedAthlete : CanonicalEntityBase<Guid>
{
    public Guid CompetitionStatusId { get; set; }

    public BaseballCompetitionStatus CompetitionStatus { get; set; } = null!;

    // ESPN's source-document ordinal (0-based). Required so consumers
    // can reconstruct ESPN's order — e.g. winningPitcher [0],
    // losingPitcher [1] — without depending on row insertion order.
    public int Ordinal { get; set; }

    public int PlayerId { get; set; }

    public string? Name { get; set; }

    public string? DisplayName { get; set; }

    public string? ShortDisplayName { get; set; }

    public string? Abbreviation { get; set; }

    // ESPN $ref pointers. Resolution to canonical Player / Franchise
    // FKs is out of scope here — stored as Uri so a downstream
    // enrichment job can join against them later.
    public Uri? AthleteRef { get; set; }

    public Uri? TeamRef { get; set; }

    public Uri? StatisticsRef { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<BaseballCompetitionStatusFeaturedAthlete>
    {
        public void Configure(EntityTypeBuilder<BaseballCompetitionStatusFeaturedAthlete> builder)
        {
            builder.ToTable(nameof(BaseballCompetitionStatusFeaturedAthlete));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Ordinal).IsRequired();
            builder.Property(x => x.PlayerId).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(100);
            builder.Property(x => x.DisplayName).HasMaxLength(100);
            builder.Property(x => x.ShortDisplayName).HasMaxLength(50);
            builder.Property(x => x.Abbreviation).HasMaxLength(20);

            builder.HasOne(x => x.CompetitionStatus)
                .WithMany(x => x.FeaturedAthletes)
                .HasForeignKey(x => x.CompetitionStatusId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
