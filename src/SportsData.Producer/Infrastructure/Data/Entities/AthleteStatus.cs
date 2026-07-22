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

        // DB-computed lowercase of Name, used to enforce CASE-INSENSITIVE
        // uniqueness (the resolver dedups by lower(name)). Store-generated, so
        // every AthleteStatus creator — the resolver and the athlete processor —
        // stays consistent without setting it. Read-only from the app.
        public string? NameNormalized { get; private set; }

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

                // Case-insensitive uniqueness: a stored computed lower(Name) plus a
                // unique index on it. Per-sport DB, so unique within the sport
                // collection. Backs the resolver's concurrent-insert guard and
                // matches its lower(name) dedup, so "Active" and "active" can't both
                // be inserted.
                builder.Property(s => s.NameNormalized)
                    .HasComputedColumnSql("lower(\"Name\")", stored: true)
                    .HasMaxLength(100);

                builder.HasIndex(s => s.NameNormalized)
                    .IsUnique();

                builder.Property(s => s.Type)
                    .HasMaxLength(50);
            }
        }
    }
}