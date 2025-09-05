using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class SeasonPollWeek : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Guid SeasonPollId { get; set; }
        public SeasonPoll SeasonPoll { get; set; } = null!;

            public Guid? SeasonWeekId { get; set; }
            public SeasonWeek? SeasonWeek { get; set; }

        // Occurrence (week#, preseason/postseason label, etc.)
        public int OccurrenceNumber { get; set; }             // dto.occurrence.number
        public string OccurrenceType { get; set; } = null!;   // "week"
        public bool OccurrenceIsLast { get; set; }
        public string OccurrenceValue { get; set; } = null!;  // "1"
        public string OccurrenceDisplay { get; set; } = null!;// "Preseason"

        // Timestamps / headlines
        public DateTime? DateUtc { get; set; }                 // dto.date
        public DateTime? LastUpdatedUtc { get; set; }          // dto.lastUpdated

        public required string Name { get; set; }
        public required string ShortName { get; set; }
        public required string Type { get; set; }

        public required string Headline { get; set; }
        public required string ShortHeadline { get; set; }

        public ICollection<SeasonPollWeekEntry> Entries { get; set; } = [];

        public ICollection<SeasonPollWeekExternalId> ExternalIds { get; set; } = [];
        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<SeasonPollWeek>
        {
            public void Configure(EntityTypeBuilder<SeasonPollWeek> builder)
            {
                builder.ToTable(nameof(SeasonPollWeek));

                builder.HasKey(w => w.Id);

                builder.Property(w => w.Id)
                    .IsRequired();

                builder.Property(w => w.SeasonPollId)
                    .IsRequired();

                builder.Property(w => w.OccurrenceNumber)
                    .IsRequired();

                builder.Property(w => w.OccurrenceType)
                    .HasMaxLength(50)
                    .IsRequired();

                builder.Property(w => w.OccurrenceValue)
                    .HasMaxLength(50)
                    .IsRequired();

                builder.Property(w => w.OccurrenceDisplay)
                    .HasMaxLength(100)
                    .IsRequired();

                builder.Property(w => w.OccurrenceIsLast)
                    .IsRequired();

                builder.Property(w => w.DateUtc);
                builder.Property(w => w.LastUpdatedUtc);

                builder.Property(w => w.Headline)
                    .HasMaxLength(200);

                builder.Property(w => w.ShortHeadline)
                    .HasMaxLength(200);

                builder.HasOne(w => w.SeasonPoll)
                    .WithMany(p => p.Weeks)
                    .HasForeignKey(w => w.SeasonPollId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);

                // ✅ Make this optional (nullable FK)
                builder.HasOne(w => w.SeasonWeek)
                    .WithMany()
                    .HasForeignKey(w => w.SeasonWeekId)
                    .OnDelete(DeleteBehavior.Restrict); // or SetNull if you want

                builder.HasMany(w => w.Entries)
                    .WithOne(e => e.SeasonPollWeek)
                    .HasForeignKey(e => e.SeasonPollWeekId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);

                // ✅ Optional: You *may* want to drop the unique index if SeasonWeekId is null in multiple rows
                // If you keep it, DBs like Postgres will only allow one NULL per unique index
                builder.HasIndex(w => new { w.SeasonPollId, w.SeasonWeekId }).IsUnique();
            }
        }

    }
}
