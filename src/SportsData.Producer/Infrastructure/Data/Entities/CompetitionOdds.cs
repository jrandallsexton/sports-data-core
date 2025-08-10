using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionOdds : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Guid CompetitionId { get; set; }

        public required Uri ProviderRef { get; set; }
        public required string ProviderId { get; set; }
        public required string ProviderName { get; set; }
        public int ProviderPriority { get; set; }

        public string? Details { get; set; }

        public decimal? OverUnder { get; set; }   // headline total
        public decimal? Spread { get; set; }      // headline spread
        public decimal? OverOdds { get; set; }    // headline over price
        public decimal? UnderOdds { get; set; }   // headline under price

        public bool? MoneylineWinner { get; set; }
        public bool? SpreadWinner { get; set; }

        // Optional: hash of source JSON payload for idempotency/change detection
        public string? ContentHash { get; set; }

        public ICollection<CompetitionTeamOdds> Teams { get; set; } = new List<CompetitionTeamOdds>();

        public ICollection<CompetitionTotalsSnapshot> Totals { get; set; } = new List<CompetitionTotalsSnapshot>();

        public ICollection<CompetitionOddsExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionOdds>
        {
            public void Configure(EntityTypeBuilder<CompetitionOdds> builder)
            {
                builder.ToTable(nameof(CompetitionOdds));
                builder.HasKey(x => x.Id);

                builder.HasIndex(x => new { x.CompetitionId, x.ProviderId }).IsUnique();

                builder.HasOne<Competition>()
                       .WithMany(x => x.Odds)
                       .HasForeignKey(x => x.CompetitionId);

                builder.Property(x => x.ProviderRef).IsRequired().HasMaxLength(256);
                builder.Property(x => x.ProviderId).IsRequired().HasMaxLength(128);
                builder.Property(x => x.ProviderName).IsRequired().HasMaxLength(128);

                builder.Property(x => x.Details).HasMaxLength(256);
                builder.Property(x => x.ContentHash).HasMaxLength(128);

                // Precision for decimals
                foreach (var p in typeof(CompetitionOdds).GetProperties()
                             .Where(p => p.PropertyType == typeof(decimal?)))
                {
                    builder.Property(p.Name).HasPrecision(18, 6);
                }

                builder.HasMany(x => x.Teams)
                       .WithOne(x => x.CompetitionOdds)
                       .HasForeignKey(x => x.CompetitionOddsId)
                       .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.Totals)
                       .WithOne()
                       .HasForeignKey(x => x.CompetitionOddsId)
                       .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
