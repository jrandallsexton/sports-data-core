using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities.Metrics
{
    public class FranchiseSeasonMetric : CanonicalEntityBase<Guid>
    {
        public Guid FranchiseSeasonId { get; set; }
        public FranchiseSeason FranchiseSeason { get; set; } = default!;

        public int Season { get; set; }
        public int GamesPlayed { get; set; }

        // Offense
        public decimal Ypp { get; set; }
        public decimal SuccessRate { get; set; }
        public decimal ExplosiveRate { get; set; }
        public decimal PointsPerDrive { get; set; }
        public decimal ThirdFourthRate { get; set; }
        public decimal? RzTdRate { get; set; }
        public decimal? RzScoreRate { get; set; }
        public decimal TimePossRatio { get; set; }

        // Defense (opponent metrics)
        public decimal OppYpp { get; set; }
        public decimal OppSuccessRate { get; set; }
        public decimal OppExplosiveRate { get; set; }
        public decimal OppPointsPerDrive { get; set; }
        public decimal OppThirdFourthRate { get; set; }
        public decimal? OppRzTdRate { get; set; }
        public decimal? OppScoreTdRate { get; set; }

        // ST / Discipline
        public decimal NetPunt { get; set; }
        public decimal FgPctShrunk { get; set; }
        public decimal FieldPosDiff { get; set; }
        public decimal TurnoverMarginPerDrive { get; set; }
        public decimal PenaltyYardsPerPlay { get; set; }

        public DateTime ComputedUtc { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonMetric>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonMetric> b)
            {
                b.ToTable(nameof(FranchiseSeasonMetric));

                b.HasKey(x => x.Id);

                b.HasIndex(x => x.FranchiseSeasonId).IsUnique();

                b.Property(x => x.Season);
                b.Property(x => x.GamesPlayed);

                // Yardage / points
                b.Property(x => x.Ypp).HasPrecision(5, 2);
                b.Property(x => x.PointsPerDrive).HasPrecision(5, 2);
                b.Property(x => x.OppYpp).HasPrecision(5, 2);
                b.Property(x => x.OppPointsPerDrive).HasPrecision(5, 2);

                // Success metrics
                b.Property(x => x.SuccessRate).HasPrecision(5, 4);
                b.Property(x => x.ExplosiveRate).HasPrecision(5, 4);
                b.Property(x => x.ThirdFourthRate).HasPrecision(5, 4);
                b.Property(x => x.RzTdRate).HasPrecision(5, 4);
                b.Property(x => x.RzScoreRate).HasPrecision(5, 4);
                b.Property(x => x.TimePossRatio).HasPrecision(5, 2);

                b.Property(x => x.OppSuccessRate).HasPrecision(5, 4);
                b.Property(x => x.OppExplosiveRate).HasPrecision(5, 4);
                b.Property(x => x.OppThirdFourthRate).HasPrecision(5, 4);
                b.Property(x => x.OppRzTdRate).HasPrecision(5, 4);
                b.Property(x => x.OppScoreTdRate).HasPrecision(5, 4);

                // ST / Discipline
                b.Property(x => x.NetPunt).HasPrecision(6, 2);
                b.Property(x => x.FgPctShrunk).HasPrecision(5, 4);
                b.Property(x => x.FieldPosDiff).HasPrecision(6, 2);
                b.Property(x => x.TurnoverMarginPerDrive).HasPrecision(6, 3);
                b.Property(x => x.PenaltyYardsPerPlay).HasPrecision(5, 2);

                b.Property(x => x.ComputedUtc);

                b.HasOne(x => x.FranchiseSeason)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseSeasonId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }

}
