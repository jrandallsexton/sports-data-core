using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities;

public class BaseballCompetitionPlay : CompetitionPlayBase
{
    public string? HalfInning { get; set; }

    public int? Outs { get; set; }

    // ESPN's wallclock — actual play time. Distinct from CompetitionPlayBase.Modified
    // (server-side last-update stamp). Useful for play timelines once we surface them.
    public DateTime? Wallclock { get; set; }

    // ESPN play "valid" flag. Default true; observed false plays are rare (overturned
    // calls, void plays). Captured so future suppression logic can gate on it.
    public bool IsValid { get; set; } = true;

    public string? AtBatId { get; set; }

    public int? AtBatPitchNumber { get; set; }

    public int? BatOrder { get; set; }

    // Batter handedness for this at-bat (RIGHT/LEFT/SWITCH). Distinct from
    // BaseballAthlete.BatsType (the athlete's general handedness): switch-hitters
    // bat the opposite way against same-handed pitchers, so the per-at-bat copy
    // captures what actually happened.
    public string? BatsType { get; set; }

    public string? BatsAbbreviation { get; set; }

    // Pitcher handedness for this at-bat (RIGHT/LEFT). Mirror of Bats* but for the
    // pitcher facing the batter. Same rationale: BaseballAthlete.ThrowsType is the
    // athlete's general handedness; this is what threw this pitch.
    public string? PitchesType { get; set; }

    public string? PitchesAbbreviation { get; set; }

    // Resolved canonical AthleteSeason IDs from participants[]. The athlete
    // ref on a play participant is season-scoped (ESPN's URL path is
    // `/seasons/{year}/athletes/{id}`), so this points at the AthleteSeason
    // row, not the global AthleteBase. Null when the AthleteSeason hasn't
    // been sourced yet (race during first ingest) — the play update path
    // re-resolves on each re-ingest.
    public Guid? AtBatAthleteSeasonId { get; set; }

    public Guid? PitchingAthleteSeasonId { get; set; }

    public double? PitchCoordinateX { get; set; }

    public double? PitchCoordinateY { get; set; }

    public double? HitCoordinateX { get; set; }

    public double? HitCoordinateY { get; set; }

    public string? PitchTypeId { get; set; }

    public string? PitchTypeText { get; set; }

    public string? PitchTypeAbbreviation { get; set; }

    public int? PitchVelocity { get; set; }

    public int? PitchCountBalls { get; set; }

    public int? PitchCountStrikes { get; set; }

    public int? ResultCountBalls { get; set; }

    public int? ResultCountStrikes { get; set; }

    public string? Trajectory { get; set; }

    public string? StrikeType { get; set; }

    public string? SummaryType { get; set; }

    public int AwayHits { get; set; }

    public int HomeHits { get; set; }

    public int AwayErrors { get; set; }

    public int HomeErrors { get; set; }

    public int RbiCount { get; set; }

    public bool IsDoublePlay { get; set; }

    public bool IsTriplePlay { get; set; }

    // Full ESPN participants[] capture, one row per participant. Backed by
    // the shared `CompetitionPlayParticipant` TPH table; the convenience
    // AtBatAthleteId / PitchingAthleteId columns above are denormalizations
    // of the primary pitcher/batter for cheap live-UI lookup.
    public ICollection<BaseballCompetitionPlayParticipant> Participants { get; set; }
        = new List<BaseballCompetitionPlayParticipant>();

    public new class EntityConfiguration : IEntityTypeConfiguration<BaseballCompetitionPlay>
    {
        public void Configure(EntityTypeBuilder<BaseballCompetitionPlay> builder)
        {
            builder.Property(x => x.HalfInning).HasMaxLength(8);
            builder.Property(x => x.AtBatId).HasMaxLength(32);
            builder.Property(x => x.BatsType).HasMaxLength(20);
            builder.Property(x => x.BatsAbbreviation).HasMaxLength(5);
            builder.Property(x => x.PitchesType).HasMaxLength(20);
            builder.Property(x => x.PitchesAbbreviation).HasMaxLength(5);
            builder.Property(x => x.PitchTypeId).HasMaxLength(10);
            builder.Property(x => x.PitchTypeText).HasMaxLength(50);
            builder.Property(x => x.PitchTypeAbbreviation).HasMaxLength(10);
            builder.Property(x => x.Trajectory).HasMaxLength(5);
            builder.Property(x => x.StrikeType).HasMaxLength(20);
            builder.Property(x => x.SummaryType).HasMaxLength(5);

            builder.Property(x => x.PitchCoordinateX).HasPrecision(7, 2);
            builder.Property(x => x.PitchCoordinateY).HasPrecision(7, 2);
            builder.Property(x => x.HitCoordinateX).HasPrecision(7, 2);
            builder.Property(x => x.HitCoordinateY).HasPrecision(7, 2);
        }
    }
}
