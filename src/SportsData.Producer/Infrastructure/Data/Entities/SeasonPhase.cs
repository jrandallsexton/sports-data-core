using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class SeasonPhase : CanonicalEntityBase<Guid>
    {
        public Guid SeasonId { get; set; }

        public Season? Season { get; set; }

        public int TypeCode { get; set; }

        public required string Name { get; set; }

        public required string Abbreviation { get; set; }

        public required string Slug { get; set; }

        public int Year { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public bool HasGroups { get; set; }

        public bool HasStandings { get; set; }

        public bool HasLegs { get; set; }

        // External IDs for deduplication/traversal
        public ICollection<SeasonPhaseExternalId> ExternalIds { get; set; } = new List<SeasonPhaseExternalId>();

        public class EntityConfiguration : IEntityTypeConfiguration<SeasonPhase>
        {
            public void Configure(EntityTypeBuilder<SeasonPhase> builder)
            {
                builder.ToTable("SeasonPhase");

                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();

                builder.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(e => e.Abbreviation)
                    .IsRequired()
                    .HasMaxLength(20);

                builder.Property(e => e.Slug)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.HasOne(e => e.Season)
                    .WithMany(s => s.Phases)
                    .HasForeignKey(e => e.SeasonId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(e => e.ExternalIds)
                    .WithOne(eid => eid.SeasonPhase)
                    .HasForeignKey(eid => eid.SeasonPhaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }

    }
}