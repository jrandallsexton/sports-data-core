using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    // Parent: SeasonWeek
    public class SeasonRanking : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Guid SeasonWeekId { get; set; }
        public SeasonWeek SeasonWeek { get; set; } = null!;

        // Poll metadata (from dto.id/name/shortName/type)
        public string ProviderPollId { get; set; } = null!;   // e.g. "2"
        public string PollName { get; set; } = null!;         // "AFCA Coaches Poll"
        public string PollShortName { get; set; } = null!;
        public string PollType { get; set; } = null!;         // "usa" | "cfp" | "ap" etc.

        // Occurrence (week#, preseason/postseason label, etc.)
        public int OccurrenceNumber { get; set; }             // dto.occurrence.number
        public string OccurrenceType { get; set; } = null!;   // "week"
        public bool OccurrenceIsLast { get; set; }
        public string OccurrenceValue { get; set; } = null!;  // "1"
        public string OccurrenceDisplay { get; set; } = null!;// "Preseason"

        // Timestamps / headlines
        public DateTime DateUtc { get; set; }                 // dto.date
        public DateTime LastUpdatedUtc { get; set; }          // dto.lastUpdated
        public string? Headline { get; set; }
        public string? ShortHeadline { get; set; }

        public ICollection<SeasonRankingEntry> Entries { get; set; } = [];
        public ICollection<SeasonRankingExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<SeasonRanking>
        {
            public void Configure(EntityTypeBuilder<SeasonRanking> builder)
            {
                builder.ToTable(nameof(SeasonRanking));

                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();

                // Uniqueness: one poll result per week/poll/date
                builder.HasIndex(e => new { e.SeasonWeekId, e.ProviderPollId, e.DateUtc })
                       .IsUnique();

                // Requireds / lengths
                builder.Property(e => e.ProviderPollId).IsRequired().HasMaxLength(32);
                builder.Property(e => e.PollName).IsRequired().HasMaxLength(128);
                builder.Property(e => e.PollShortName).HasMaxLength(64);
                builder.Property(e => e.PollType).IsRequired().HasMaxLength(32);

                builder.Property(e => e.Headline).HasMaxLength(512);
                builder.Property(e => e.ShortHeadline).HasMaxLength(256);

                builder.Property(e => e.OccurrenceType).IsRequired().HasMaxLength(32);
                builder.Property(e => e.OccurrenceValue).IsRequired().HasMaxLength(32);
                builder.Property(e => e.OccurrenceDisplay).IsRequired().HasMaxLength(64);

                builder.Property(e => e.DateUtc).IsRequired();
                builder.Property(e => e.LastUpdatedUtc).IsRequired();

                // Relationships
                builder.HasOne(e => e.SeasonWeek)
                       .WithMany(w => w.Rankings)
                       .HasForeignKey(e => e.SeasonWeekId)
                       .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(e => e.Entries)
                       .WithOne(r => r.SeasonRanking)
                       .HasForeignKey(r => r.SeasonRankingId)
                       .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(e => e.ExternalIds)
                       .WithOne(x => x.SeasonRanking)
                       .HasForeignKey(x => x.SeasonRankingId)
                       .OnDelete(DeleteBehavior.Cascade);

                builder.Navigation(e => e.Entries).AutoInclude(false);
                builder.Navigation(e => e.ExternalIds).AutoInclude(false);
            }
        }
    }
}
