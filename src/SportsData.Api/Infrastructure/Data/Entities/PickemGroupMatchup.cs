using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class PickemGroupMatchup : CanonicalEntityBase<Guid>
    {
        public Guid GroupId { get; set; }

        public Guid ContestId { get; set; }

        public string? Headline { get; set; }

        public DateTime StartDateUtc { get; set; }

        public int SeasonYear { get; set; }

        public int SeasonWeek { get; set; }

        public string? Spread { get; set; }

        public double? AwaySpread { get; set; }

        public int? AwayRank { get; set; }

        public double? HomeSpread { get; set; }

        public int? HomeRank { get; set; }

        public double? OverUnder { get; set; }

        public double? OverOdds { get; set; }

        public double? UnderOdds { get; set; }

        // Record snapshots (records at the time of matchup generation)
        public int AwayWins { get; set; }

        public int AwayLosses { get; set; }

        public int AwayTies { get; set; }

        public int AwayConferenceWins { get; set; }

        public int AwayConferenceLosses { get; set; }

        public int AwayConferenceTies { get; set; }

        public int HomeWins { get; set; }

        public int HomeLosses { get; set; }

        public int HomeTies { get; set; }

        public int HomeConferenceWins { get; set; }

        public int HomeConferenceLosses { get; set; }

        public int HomeConferenceTies { get; set; }

        // Nav back to PickemGroupWeek (optional but recommended)
        public PickemGroupWeek GroupWeek { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<PickemGroupMatchup>
        {
            public void Configure(EntityTypeBuilder<PickemGroupMatchup> builder)
            {
                builder.ToTable(nameof(PickemGroupMatchup));
                builder.HasKey(x => new { x.GroupId, x.SeasonWeekId, x.ContestId });

                builder.Property(x => x.Headline).HasMaxLength(256);

                builder.Property(x => x.SeasonYear).IsRequired();
                builder.Property(x => x.SeasonWeek).IsRequired();
                builder.Property(x => x.ContestId).IsRequired();
                builder.Property(x => x.GroupId).IsRequired();
                builder.Property(x => x.StartDateUtc).IsRequired();

                builder.Property(x => x.AwaySpread).HasPrecision(10, 2);
                builder.Property(x => x.HomeSpread).HasPrecision(10, 2);
                builder.Property(x => x.OverUnder).HasPrecision(10, 2);

                // Unique matchup constraint for a group
                builder.HasIndex(x => new { x.GroupId, x.ContestId }).IsUnique();

                // Useful index for resolving all matchups by group/week
                builder.HasIndex(x => new { x.GroupId, x.SeasonYear, x.SeasonWeek });

                // Composite FK to PickemGroupWeek (GroupId + SeasonWeekId)
                builder
                    .HasOne(x => x.GroupWeek)
                    .WithMany(x => x.Matchups)
                    .HasForeignKey(x => new { x.GroupId, x.SeasonWeekId })
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }

        // Required to enable the FK above (not shown in your current model)
        public Guid SeasonWeekId { get; set; } // <-- Add this to the class if not already present
    }
}
