using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class AthleteStatus : CanonicalEntityBase<Guid>
    {
        public string ExternalId { get; set; } = default!;

        public string? Abbreviation { get; set; }

        public string? Name { get; set; }

        // Canonical lowercase of Name (ToLowerInvariant), used to enforce
        // CASE-INSENSITIVE uniqueness and drive all status lookups. Set by every
        // creator (AthleteStatusResolver + the athlete processor) with the same
        // rule, so lookup and constraint can't diverge for culture/Unicode cases.
        public string? NameNormalized { get; set; }

        public string? Type { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<AthleteStatus>
        {
            public void Configure(EntityTypeBuilder<AthleteStatus> builder)
            {
                builder.ToTable(nameof(AthleteStatus));

                builder.HasKey(s => s.Id);

                builder.Property(s => s.ExternalId)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(s => s.Abbreviation)
                    .HasMaxLength(50);

                builder.Property(s => s.Name)
                    .HasMaxLength(100);

                // Case-insensitive uniqueness (per-sport DB, so unique within the
                // sport collection). Backs the resolver's concurrent-insert guard;
                // creators set NameNormalized = ToLowerInvariant(Name), so "Active"
                // and "active" can't both be inserted.
                builder.Property(s => s.NameNormalized)
                    .HasMaxLength(100);

                builder.HasIndex(s => s.NameNormalized)
                    .IsUnique();

                builder.Property(s => s.Type)
                    .HasMaxLength(50);
            }
        }
    }
}