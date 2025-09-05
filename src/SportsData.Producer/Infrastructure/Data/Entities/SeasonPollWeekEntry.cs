using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

/// <summary>
/// One row per team shown in the poll (Top 25 + “others receiving votes”)
/// </summary>
public class SeasonPollWeekEntry : CanonicalEntityBase<Guid>
{
    public Guid SeasonPollWeekId { get; set; }
    public SeasonPollWeek SeasonPollWeek { get; set; } = null!;

    // "ranks" | "others"
    public required string SourceList { get; set; } = "ranks";

    // Rank row
    public int Current { get; set; }                  // 1..25 or 0 for “others”
    public int Previous { get; set; }
    public double Points { get; set; }               // votes/points
    public int FirstPlaceVotes { get; set; }
    public string Trend { get; set; } = string.Empty;          // "-2", "+2" or up/down indicator
    public bool IsOtherReceivingVotes { get; set; }
    public bool IsDroppedOut { get; set; }

    // Team linkage — now required to enforce uniqueness correctly
    public Guid FranchiseSeasonId { get; set; }
    public FranchiseSeason FranchiseSeason { get; set; } = null!;

    // Record snapshot
    public string? RecordSummary { get; set; }        // "0-0"
    public int? Wins { get; set; }
    public int? Losses { get; set; }

    public DateTime RowDateUtc { get; set; }         // dto.date (UTC)
    public DateTime RowLastUpdatedUtc { get; set; }  // dto.lastUpdated (UTC)

    public ICollection<SeasonPollWeekEntryStat> Stats { get; set; } = [];

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonPollWeekEntry>
    {
        public void Configure(EntityTypeBuilder<SeasonPollWeekEntry> builder)
        {
            builder.ToTable(nameof(SeasonPollWeekEntry));

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            // Required properties and lengths
            builder.Property(e => e.SourceList)
                .IsRequired()
                .HasMaxLength(16);

            builder.Property(e => e.Trend)
                .HasMaxLength(8);

            builder.Property(e => e.RecordSummary)
                .HasMaxLength(32);

            builder.Property(e => e.Points)
                .HasPrecision(18, 6);

            // Relationships
            builder.HasOne(e => e.SeasonPollWeek)
                .WithMany(w => w.Entries)
                .HasForeignKey(e => e.SeasonPollWeekId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.FranchiseSeason)
                .WithMany()
                .HasForeignKey(e => e.FranchiseSeasonId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(e => e.Stats)
                .WithOne(s => s.PollWeekEntry)
                .HasForeignKey(s => s.SeasonPollWeekEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint per poll occurrence, team-season, and source list
            builder.HasIndex(e => new { e.SeasonPollWeekId, e.FranchiseSeasonId, e.SourceList })
                .IsUnique();
        }
    }

}
