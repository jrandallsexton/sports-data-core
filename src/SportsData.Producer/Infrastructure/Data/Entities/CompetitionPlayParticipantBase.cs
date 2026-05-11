using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

/// <summary>
/// Shared base for per-play participant rows. Mirrors the
/// `CompetitionPlayBase` / `BaseballCompetitionPlay` / `FootballCompetitionPlay`
/// TPH split: one sport-agnostic table (`CompetitionPlayParticipant`) backs
/// all sport-specific subclasses, with EF auto-generating the discriminator.
///
/// The FK back to the play and the navigation collection live on the
/// sport-specific subclass + sport-specific `CompetitionPlay` derived type
/// — only sports that ship a participant subclass register the relationship,
/// so a sport without participants doesn't drag the abstract base into its
/// model.
///
/// Captures the full ESPN participants[] entry verbatim so we don't lose
/// data when a referenced athlete or position hasn't been sourced yet —
/// the play update path re-resolves AthleteId on each re-ingest, and the
/// preserved refs stay available for future re-resolution / pipelines.
/// </summary>
public abstract class CompetitionPlayParticipantBase : CanonicalEntityBase<Guid>
{
    public Guid CompetitionPlayId { get; set; }

    public int Order { get; set; }

    // ESPN-supplied participant role. "pitcher" / "batter" for baseball today;
    // future taxonomy entries land here verbatim. Processors log a warning
    // on unrecognized types but still persist the row.
    public string Type { get; set; } = string.Empty;

    // Resolved canonical IDs. Null when the referenced doc hasn't been
    // sourced yet — re-resolves on the next play update.
    //
    // The athlete ref on a play participant is a SEASON-scoped athlete
    // URL (`/seasons/{year}/athletes/{id}`), so this resolves to an
    // AthleteSeason row (not the global AthleteBase). ESPN names the
    // JSON field "athlete" which is misleading; the path tells the truth.
    public Guid? AthleteSeasonId { get; set; }

    public Guid? PositionId { get; set; }

    // Per-play participant statistics ref. Future processor target
    // (batter-vs-pitcher splits, pitch-by-pitch outcomes); stays a string
    // until those docs get their own canonical pipeline.
    public string? StatisticsRef { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<CompetitionPlayParticipantBase>
    {
        public void Configure(EntityTypeBuilder<CompetitionPlayParticipantBase> builder)
        {
            builder.ToTable("CompetitionPlayParticipant");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Type).IsRequired().HasMaxLength(32);
            builder.Property(t => t.StatisticsRef).HasMaxLength(512);

            builder.HasIndex(t => new { t.CompetitionPlayId, t.Type });
            builder.HasIndex(t => t.AthleteSeasonId);
            builder.HasIndex(t => t.PositionId);
        }
    }
}
