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

    // "ranks" | "others"
    public required string SourceList { get; set; } = "ranks";

    // Rank row
    public int Current { get; set; }                  // 1..25 or 0 for “others”
    public int Previous { get; set; }
    public decimal Points { get; set; }               // votes/points
    public int FirstPlaceVotes { get; set; }
    public string Trend { get; set; } = "-";          // "-" or up/down indicator
    public bool IsOtherReceivingVotes { get; set; }   // consider deriving from SourceList

    // Team linkage — now required to enforce uniqueness correctly
    public Guid FranchiseSeasonId { get; set; }
    public FranchiseSeason FranchiseSeason { get; set; } = null!;

    // Record snapshot
    public string? RecordSummary { get; set; }        // "0-0"
    public int? Wins { get; set; }
    public int? Losses { get; set; }

    public DateTime? RowDateUtc { get; set; }         // dto.date (UTC)
    public DateTime? RowLastUpdatedUtc { get; set; }  // dto.lastUpdated (UTC)

    public ICollection<SeasonRankingEntryStat> Stats { get; set; } = [];

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonRankingEntry>
    {
        public void Configure(EntityTypeBuilder<SeasonRankingEntry> builder)
        {
            builder.ToTable(nameof(SeasonRankingEntry));

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            // Requireds / lengths
            builder.Property(e => e.SourceList)
                   .IsRequired()
                   .HasMaxLength(16);

            builder.Property(e => e.Trend).HasMaxLength(8);
            builder.Property(e => e.RecordSummary).HasMaxLength(32);

            // Provider-agnostic decimal precision
            builder.Property(e => e.Points).HasPrecision(18, 6);

            // Relationships
            builder.HasOne(e => e.SeasonRanking)
                   .WithMany(r => r.Entries)
                   .HasForeignKey(e => e.SeasonRankingId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.FranchiseSeason)
                   .WithMany() // or .WithMany(fs => fs.SeasonRankingEntries)
                   .HasForeignKey(e => e.FranchiseSeasonId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(e => e.Stats)
                   .WithOne(s => s.Entry)
                   .HasForeignKey(s => s.SeasonRankingEntryId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Strict uniqueness per poll occurrence, team-season, and source list
            builder.HasIndex(e => new { e.SeasonRankingId, e.FranchiseSeasonId, e.SourceList })
                   .IsUnique();
        }
    }
}
