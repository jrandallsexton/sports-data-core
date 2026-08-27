using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class PickemGroupWeek : CanonicalEntityBase<Guid>
    {
        public Guid GroupId { get; set; }

        public PickemGroup Group { get; set; } = null!;

        public required int SeasonYear { get; set; }

        public required int SeasonWeek { get; set; }

        public required Guid SeasonWeekId { get; set; }

        /// <summary>
        /// SeasonPhase.TypeCode of this week: 1 Preseason, 2 Regular
        /// Season, 3 Postseason. Week NUMBERS repeat across phases within
        /// a season year (an NFL league can hold a preseason Week 4 AND a
        /// regular-season Week 4), so week identity shown to users — and
        /// in URLs — needs the phase. Stamped by MatchupScheduleProcessor
        /// from canonical matchup data; default 2 covers pre-existing
        /// rows, corrected by the one-time backfill
        /// (sql/pgsql/backfill_pickemgroupweek_phase.sql).
        /// </summary>
        public int SeasonPhaseTypeCode { get; set; } = 2;

        public bool IsNonStandardWeek { get; set; }

        public bool AreMatchupsGenerated { get; set; }

        public ICollection<PickemGroupMatchup> Matchups { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<PickemGroupWeek>
        {
            public void Configure(EntityTypeBuilder<PickemGroupWeek> builder)
            {
                builder.ToTable(nameof(PickemGroupWeek));

                builder.HasKey(x => new { x.GroupId, x.SeasonWeekId }); // Composite PK

                builder.Property(x => x.SeasonYear).IsRequired();
                builder.Property(x => x.SeasonWeek).IsRequired();
                builder.Property(x => x.SeasonWeekId).IsRequired();
                builder.Property(x => x.SeasonPhaseTypeCode).IsRequired().HasDefaultValue(2);
                builder.Property(x => x.IsNonStandardWeek).IsRequired();

                builder.Property(x => x.AreMatchupsGenerated).IsRequired();

                builder
                    .HasOne(x => x.Group)
                    .WithMany(x => x.Weeks)
                    .HasForeignKey(x => x.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder
                    .HasMany(x => x.Matchups)
                    .WithOne(x => x.GroupWeek)
                    .HasForeignKey(x => new { x.GroupId, x.SeasonWeekId })
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasIndex(x => new { x.SeasonYear, x.SeasonWeek });
            }
        }
    }
}