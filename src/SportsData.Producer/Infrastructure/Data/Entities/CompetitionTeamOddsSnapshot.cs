using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionTeamOddsSnapshot : CanonicalEntityBase<Guid>
    {
        public Guid TeamOddsId { get; set; }          // FK -> CompetitionTeamOdds
        public string Phase { get; set; } = null!;    // "Open" | "Close" | "Current"

        // Phase-aware favorite/underdog (captures flips over time)
        public bool? IsFavorite { get; set; }
        public bool? IsUnderdog { get; set; }

        // Point spread LINE (e.g., "+4.5", "-3.5")
        public string? PointSpreadRaw { get; set; }
        public decimal? PointSpreadNum { get; set; }

        // Spread PRICE
        public decimal? SpreadValue { get; set; }
        public string? SpreadDisplay { get; set; }
        public string? SpreadAlt { get; set; }
        public decimal? SpreadDecimal { get; set; }
        public string? SpreadFraction { get; set; }
        public string? SpreadAmerican { get; set; }
        public string? SpreadOutcome { get; set; }

        // Moneyline PRICE
        public decimal? MoneylineValue { get; set; }
        public string? MoneylineDisplay { get; set; }
        public string? MoneylineAlt { get; set; }
        public decimal? MoneylineDecimal { get; set; }
        public string? MoneylineFraction { get; set; }
        public string? MoneylineAmerican { get; set; }   // raw ("EVEN", "+195", "-230")
        public string? MoneylineOutcome { get; set; }

        // Optional numeric normalization (e.g., "EVEN" => +100)
        public int? MoneylineAmericanNum { get; set; }

        // Provenance
        public DateTime FetchedUtc { get; set; }         // set by ingester
        public string? SourceUrlHash { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionTeamOddsSnapshot>
        {
            public void Configure(EntityTypeBuilder<CompetitionTeamOddsSnapshot> builder)
            {
                builder.ToTable(nameof(CompetitionTeamOddsSnapshot));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.Phase).IsRequired().HasMaxLength(16);

                builder.Property(x => x.PointSpreadRaw).HasMaxLength(64);
                builder.Property(x => x.SpreadDisplay).HasMaxLength(256);
                builder.Property(x => x.SpreadAlt).HasMaxLength(256);
                builder.Property(x => x.SpreadFraction).HasMaxLength(256);
                builder.Property(x => x.SpreadAmerican).HasMaxLength(256);
                builder.Property(x => x.SpreadOutcome).HasMaxLength(64);

                builder.Property(x => x.MoneylineDisplay).HasMaxLength(256);
                builder.Property(x => x.MoneylineAlt).HasMaxLength(256);
                builder.Property(x => x.MoneylineFraction).HasMaxLength(256);
                builder.Property(x => x.MoneylineAmerican).HasMaxLength(256);
                builder.Property(x => x.MoneylineOutcome).HasMaxLength(64);

                builder.Property(x => x.SourceUrlHash).HasMaxLength(128);

                // all decimal? fields -> precision
                foreach (var p in typeof(CompetitionTeamOddsSnapshot).GetProperties()
                             .Where(p => p.PropertyType == typeof(decimal?)))
                {
                    builder.Property(p.Name).HasPrecision(18, 6);
                }

                builder.HasOne<CompetitionTeamOdds>()
                       .WithMany(x => x.Snapshots)
                       .HasForeignKey(x => x.TeamOddsId)
                       .OnDelete(DeleteBehavior.Cascade);

                // Phase uniqueness per side/provider
                builder.HasIndex(x => new { x.TeamOddsId, x.Phase }).IsUnique();
            }
        }
    }
}
