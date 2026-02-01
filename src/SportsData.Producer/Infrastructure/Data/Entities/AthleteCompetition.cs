using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

/// <summary>
/// Represents an athlete's participation in a specific competition (game).
/// Captures roster information including position, jersey number, and active status.
/// Critical for calculating Games Played statistic and tracking player availability.
/// </summary>
public class AthleteCompetition : CanonicalEntityBase<Guid>
{
    public Guid CompetitionId { get; set; }

    public Competition Competition { get; set; } = null!;

    public Guid CompetitionCompetitorId { get; set; }

    public CompetitionCompetitor CompetitionCompetitor { get; set; } = null!;

    public Guid AthleteSeasonId { get; set; }

    public AthleteSeason AthleteSeason { get; set; } = null!;

    /// <summary>
    /// Position player held for this specific competition.
    /// Nullable to support cases where position data is missing or unavailable.
    /// </summary>
    public Guid? PositionId { get; set; }

    public AthletePosition? Position { get; set; }

    /// <summary>
    /// Jersey number worn by the athlete in this competition.
    /// Nullable as jersey numbers may not always be available or may change.
    /// </summary>
    public string? JerseyNumber { get; set; }

    /// <summary>
    /// Indicates whether the athlete was inactive for this competition.
    /// True = Did Not Play (injury, suspension, coach's decision, etc.)
    /// False = Active on game roster (may or may not have played)
    /// </summary>
    public bool DidNotPlay { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<AthleteCompetition>
    {
        public void Configure(EntityTypeBuilder<AthleteCompetition> builder)
        {
            builder.ToTable(nameof(AthleteCompetition));
            builder.HasKey(x => x.Id);

            // Composite unique index: one roster entry per athlete per competition per competitor
            builder.HasIndex(x => new { x.CompetitionId, x.CompetitionCompetitorId, x.AthleteSeasonId })
                .IsUnique();

            builder.Property(x => x.JerseyNumber)
                .HasMaxLength(10);

            builder.HasOne(x => x.Competition)
                .WithMany()
                .HasForeignKey(x => x.CompetitionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.CompetitionCompetitor)
                .WithMany()
                .HasForeignKey(x => x.CompetitionCompetitorId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.AthleteSeason)
                .WithMany()
                .HasForeignKey(x => x.AthleteSeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
