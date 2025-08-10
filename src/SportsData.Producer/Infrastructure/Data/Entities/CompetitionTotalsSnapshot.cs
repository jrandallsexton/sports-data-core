using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionTotalsSnapshot : CanonicalEntityBase<Guid>
    {
        public Guid CompetitionOddsId { get; set; }       // FK -> CompetitionOdds
        public string Phase { get; set; } = null!;        // "Open" | "Close" | "Current"

        // Over
        public decimal? OverValue { get; set; }
        public string? OverDisplay { get; set; }
        public string? OverAlt { get; set; }
        public decimal? OverDecimal { get; set; }
        public string? OverFraction { get; set; }
        public string? OverAmerican { get; set; }
        public string? OverOutcome { get; set; }

        // Under
        public decimal? UnderValue { get; set; }
        public string? UnderDisplay { get; set; }
        public string? UnderAlt { get; set; }
        public decimal? UnderDecimal { get; set; }
        public string? UnderFraction { get; set; }
        public string? UnderAmerican { get; set; }
        public string? UnderOutcome { get; set; }

        // Total line
        public decimal? TotalValue { get; set; }
        public string? TotalDisplay { get; set; }
        public string? TotalAlt { get; set; }
        public decimal? TotalDecimal { get; set; }
        public string? TotalFraction { get; set; }
        public string? TotalAmerican { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionTotalsSnapshot>
        {
            public void Configure(EntityTypeBuilder<CompetitionTotalsSnapshot> builder)
            {
                builder.ToTable(nameof(CompetitionTotalsSnapshot));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.Phase).IsRequired().HasMaxLength(16);

                builder.Property(x => x.OverDisplay).HasMaxLength(256);
                builder.Property(x => x.OverAlt).HasMaxLength(256);
                builder.Property(x => x.OverFraction).HasMaxLength(256);
                builder.Property(x => x.OverAmerican).HasMaxLength(256);
                builder.Property(x => x.OverOutcome).HasMaxLength(64);

                builder.Property(x => x.UnderDisplay).HasMaxLength(256);
                builder.Property(x => x.UnderAlt).HasMaxLength(256);
                builder.Property(x => x.UnderFraction).HasMaxLength(256);
                builder.Property(x => x.UnderAmerican).HasMaxLength(256);
                builder.Property(x => x.UnderOutcome).HasMaxLength(64);

                builder.Property(x => x.TotalDisplay).HasMaxLength(256);
                builder.Property(x => x.TotalAlt).HasMaxLength(256);
                builder.Property(x => x.TotalFraction).HasMaxLength(256);
                builder.Property(x => x.TotalAmerican).HasMaxLength(256);

                builder.HasOne<CompetitionOdds>()
                    .WithMany() // parent doesn’t need nav for now
                    .HasForeignKey(x => x.CompetitionOddsId)
                    .OnDelete(DeleteBehavior.Cascade);

                foreach (var p in typeof(CompetitionTotalsSnapshot).GetProperties()
                             .Where(p => p.PropertyType == typeof(decimal?)))
                {
                    builder.Property(p.Name).HasPrecision(18, 6);
                }
            }
        }
    }
}
