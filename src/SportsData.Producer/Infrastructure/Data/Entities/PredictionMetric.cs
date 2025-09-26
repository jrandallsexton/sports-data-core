using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class PredictionMetric : CanonicalEntityBase<Guid>
    {
        public string Name { get; set; } = null!;                // e.g. "teamOffEff"

        public string DisplayName { get; set; } = null!;         // e.g. "Team Offensive Efficiency"
        
        public string? ShortDisplayName { get; set; }            // Optional UI label
        
        public string Abbreviation { get; set; } = null!;        // e.g. "TOE"
        
        public string? Description { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<PredictionMetric>
        {
            public void Configure(EntityTypeBuilder<PredictionMetric> builder)
            {
                builder.ToTable(nameof(PredictionMetric));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
                builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
                builder.Property(x => x.ShortDisplayName).HasMaxLength(100);
                builder.Property(x => x.Abbreviation).IsRequired().HasMaxLength(20);
                builder.Property(x => x.Description).HasMaxLength(500);

                // Prevent *exact* duplicate records across the descriptor set
                builder.HasIndex(x => new
                {
                    x.Name,
                    x.DisplayName,
                    x.ShortDisplayName,
                    x.Abbreviation,
                    x.Description
                }).IsUnique();

                // (Optional but usually wise)
                // Enforce uniqueness on Name individually as well.
                builder.HasIndex(x => x.Name).IsUnique();
                builder.HasIndex(x => x.Abbreviation);

                // If you need case-insensitive uniqueness, see note below.
            }
        }
    }
}