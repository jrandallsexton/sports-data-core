using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

public class CompetitionOdds : CanonicalEntityBase<Guid>, IHasExternalIds
{
    public Guid CompetitionId { get; set; }

    public required Uri ProviderRef { get; set; }
    public required string ProviderId { get; set; }
    public required string ProviderName { get; set; }
    public int ProviderPriority { get; set; }

    public string? Details { get; set; }

    // Headline (current) values
    public decimal? OverUnder { get; set; }
    public decimal? Spread { get; set; }
    public decimal? OverOdds { get; set; }
    public decimal? UnderOdds { get; set; }

    // Totals (game O/U) — open/current/close
    public decimal? TotalPointsOpen { get; set; }
    public decimal? OverPriceOpen { get; set; }
    public decimal? UnderPriceOpen { get; set; }

    public decimal? TotalPointsCurrent { get; set; }
    public decimal? OverPriceCurrent { get; set; }
    public decimal? UnderPriceCurrent { get; set; }

    public decimal? TotalPointsClose { get; set; }
    public decimal? OverPriceClose { get; set; }
    public decimal? UnderPriceClose { get; set; }


    public bool? MoneylineWinner { get; set; }
    public bool? SpreadWinner { get; set; }

    public ICollection<CompetitionOddsLink> Links { get; set; } = [];
    public Uri? PropBetsRef { get; set; }
    public string? ContentHash { get; set; }

    public ICollection<CompetitionTeamOdds> Teams { get; set; } = [];

    // If you already have ClosedUtc, keep it. Add:
    public DateTime? ClosedUtc { get; set; }
    public DateTime? CorrectedUtc { get; set; }  // ← NEW

    public ICollection<CompetitionOddsExternalId> ExternalIds { get; set; } = [];
    public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

    public class EntityConfiguration : IEntityTypeConfiguration<CompetitionOdds>
    {
        public void Configure(EntityTypeBuilder<CompetitionOdds> builder)
        {
            builder.ToTable(nameof(CompetitionOdds));
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => new { x.CompetitionId, x.ProviderId }).IsUnique();
            builder.HasIndex(x => x.CompetitionId);

            builder.HasOne<Competition>()
                   .WithMany(x => x.Odds)
                   .HasForeignKey(x => x.CompetitionId);

            builder.Property(x => x.ProviderRef).IsRequired().HasMaxLength(256);
            builder.Property(x => x.ProviderId).IsRequired().HasMaxLength(128);
            builder.Property(x => x.ProviderName).IsRequired().HasMaxLength(128);

            builder.Property(x => x.Details).HasMaxLength(256);
            builder.Property(x => x.ContentHash).HasMaxLength(128);
            builder.Property(x => x.PropBetsRef).HasMaxLength(256);

            foreach (var p in typeof(CompetitionOdds).GetProperties()
                         .Where(p => p.PropertyType == typeof(decimal?)))
            {
                builder.Property(p.Name).HasPrecision(18, 6);
            }

            builder.HasMany(x => x.Teams)
                   .WithOne(x => x.CompetitionOdds)
                   .HasForeignKey(x => x.CompetitionOddsId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Links)
                   .WithOne()
                   .HasForeignKey(x => x.CompetitionOddsId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Removed: Totals snapshots relationship
        }
    }
}
