using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities;

public class BaseballCompetitionPlay : CompetitionPlay
{
    public string? AtBatId { get; set; }

    public int? AtBatPitchNumber { get; set; }

    public int? BatOrder { get; set; }

    public string? BatsType { get; set; }

    public string? BatsAbbreviation { get; set; }

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

    public new class EntityConfiguration : IEntityTypeConfiguration<BaseballCompetitionPlay>
    {
        public void Configure(EntityTypeBuilder<BaseballCompetitionPlay> builder)
        {
            builder.Property(x => x.AtBatId).HasMaxLength(32);
            builder.Property(x => x.BatsType).HasMaxLength(20);
            builder.Property(x => x.BatsAbbreviation).HasMaxLength(5);
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
