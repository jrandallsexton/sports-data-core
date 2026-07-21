using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Football.Entities;

public class FootballCompetitionPlay : CompetitionPlayBase
{
    public CompetitionDrive? Drive { get; set; }

    public Guid? DriveId { get; set; }

    public double ClockValue { get; set; }

    public string? ClockDisplayValue { get; set; }

    public Guid? EndFranchiseSeasonId { get; set; }

    public int? StartDown { get; set; }

    public int? StartDistance { get; set; }

    public int? StartYardLine { get; set; }

    public int? StartYardsToEndzone { get; set; }

    public int? EndDown { get; set; }

    public int? EndDistance { get; set; }

    public int? EndYardLine { get; set; }

    public int? EndYardsToEndzone { get; set; }

    public int StatYardage { get; set; }

    // Fields ESPN ships on football plays that the mapper previously dropped.
    // See docs/features/espn-processor-data-capture-audit.md.

    // Real-world timestamp of the play (baseball already keeps one).
    public DateTime? Wallclock { get; set; }

    // Scoring-play type label (TD / FG / safety / …). Distinguishes what kind of
    // score a play was without parsing Text; complements ScoringPlay + ScoreValue.
    public string? ScoringTypeName { get; set; }

    public string? ScoringTypeDisplayName { get; set; }

    public string? ScoringTypeAbbreviation { get; set; }

    // Point-after-attempt / two-point conversion result.
    public int? PointAfterAttemptId { get; set; }

    public string? PointAfterAttemptText { get; set; }

    public string? PointAfterAttemptAbbreviation { get; set; }

    public int? PointAfterAttemptValue { get; set; }

    public new class EntityConfiguration : IEntityTypeConfiguration<FootballCompetitionPlay>
    {
        public void Configure(EntityTypeBuilder<FootballCompetitionPlay> builder)
        {
            builder.Property(x => x.ClockDisplayValue).HasMaxLength(32);
            builder.Property(x => x.DriveId).IsRequired(false);
            builder.Property(x => x.ScoringTypeName).HasMaxLength(50);
            builder.Property(x => x.ScoringTypeDisplayName).HasMaxLength(100);
            builder.Property(x => x.ScoringTypeAbbreviation).HasMaxLength(20);
            builder.Property(x => x.PointAfterAttemptText).HasMaxLength(100);
            builder.Property(x => x.PointAfterAttemptAbbreviation).HasMaxLength(20);

            builder.HasOne(x => x.Drive)
                .WithMany(x => x.Plays)
                .HasForeignKey(x => x.DriveId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
