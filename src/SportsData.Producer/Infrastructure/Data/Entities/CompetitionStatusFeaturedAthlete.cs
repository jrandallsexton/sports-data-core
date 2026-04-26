using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    // MLB-only child of CompetitionStatus. ESPN's MLB status payload carries
    // a "featuredAthletes" collection (current pitcher, batter, etc.) tied
    // to the at-bat snapshot. The football payload has no analogue, so this
    // table will simply be empty for football contexts.
    public class CompetitionStatusFeaturedAthlete : CanonicalEntityBase<Guid>
    {
        public Guid CompetitionStatusId { get; set; }

        public CompetitionStatus CompetitionStatus { get; set; } = null!;

        // Source-document ordinal (0-based). ESPN's featuredAthletes array
        // is semantically ordered (e.g. winningPitcher then losingPitcher
        // post-game; current batter then pitcher in-game). Required so
        // consumers can reconstruct that order without depending on row
        // insertion order.
        public int Ordinal { get; set; }

        public int PlayerId { get; set; }

        public string? Name { get; set; }

        public string? DisplayName { get; set; }

        public string? ShortDisplayName { get; set; }

        public string? Abbreviation { get; set; }

        // ESPN $ref pointers; resolution to canonical Player/Franchise FKs is
        // out of scope for this iteration — stored as Uri so downstream
        // enrichment can join against them later.
        public Uri? AthleteRef { get; set; }

        public Uri? TeamRef { get; set; }

        public Uri? StatisticsRef { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionStatusFeaturedAthlete>
        {
            public void Configure(EntityTypeBuilder<CompetitionStatusFeaturedAthlete> builder)
            {
                builder.ToTable(nameof(CompetitionStatusFeaturedAthlete));
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
}
