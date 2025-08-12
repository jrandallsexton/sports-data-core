using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

/// <summary>
/// One row per team shown in the poll (Top 25 + “others receiving votes”)
/// </summary>
public class SeasonRankingEntry : CanonicalEntityBase<Guid>
{
    public Guid SeasonRankingId { get; set; }
    public SeasonRanking SeasonRanking { get; set; } = null!;

    // Rank row
    public int Current { get; set; }                      // 1..25 or 0 for “others”
    public int Previous { get; set; }
    public decimal Points { get; set; }                   // votes/points
    public int FirstPlaceVotes { get; set; }
    public string Trend { get; set; } = "-";              // "-" or up/down indicator
    public bool IsOtherReceivingVotes { get; set; }       // true for the “others” list

    // Team linkage — resolve to Franchise (or FranchiseSeason) when available
    public Guid? FranchiseId { get; set; }                // nullable until resolved
    public Guid? FranchiseSeasonId { get; set; }
    public string TeamRefUrlHash { get; set; } = null!;   // stable key from ESPN $ref

    // Record snapshot (kept flexible)
    public string? RecordSummary { get; set; }            // "0-0"
    public int? Wins { get; set; }
    public int? Losses { get; set; }

    public DateTime RowDateUtc { get; set; }              // dto.date
    public DateTime RowLastUpdatedUtc { get; set; }       // dto.lastUpdated

    // Optional: keep arbitrary stats (if you want full fidelity)
    public ICollection<SeasonRankingEntryStat> Stats { get; set; } = [];

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonRankingEntry>
    {
        public void Configure(EntityTypeBuilder<SeasonRankingEntry> builder)
        {
            builder.ToTable(nameof(SeasonRankingEntry));

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            // Requireds / lengths
            builder.Property(e => e.TeamRefUrlHash).IsRequired().HasMaxLength(128);
            builder.Property(e => e.Trend).HasMaxLength(8);
            builder.Property(e => e.RecordSummary).HasMaxLength(32);

            builder.Property(e => e.Points).HasColumnType("decimal(10,2)");

            builder.Property(e => e.RowDateUtc).IsRequired();
            builder.Property(e => e.RowLastUpdatedUtc).IsRequired();

            // Relationships
            builder.HasOne(e => e.SeasonRanking)
                .WithMany(r => r.Entries)
                .HasForeignKey(e => e.SeasonRankingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.Stats)
                .WithOne(s => s.Entry)
                .HasForeignKey(s => s.SeasonRankingEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes / uniqueness
            builder.HasIndex(e => new { e.SeasonRankingId, e.TeamRefUrlHash }).IsUnique();
            builder.HasIndex(e => new { e.SeasonRankingId, e.Current }); // optional query aid
        }
    }
}