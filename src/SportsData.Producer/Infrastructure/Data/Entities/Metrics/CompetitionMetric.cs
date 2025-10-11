using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities.Metrics
{
    public class CompetitionMetric
    {
        public Guid CompetitionId { get; set; }

        public Guid FranchiseSeasonId { get; set; }

        public int Season { get; set; }

        // Offense
        public decimal Ypp { get; set; }
        public decimal SuccessRate { get; set; }
        public decimal ExplosiveRate { get; set; }
        public decimal PointsPerDrive { get; set; }
        public decimal ThirdFourthRate { get; set; }
        public decimal? RzTdRate { get; set; }

        // Defense (opponent perspective)
        public decimal OppYpp { get; set; }
        public decimal OppSuccessRate { get; set; }
        public decimal OppExplosiveRate { get; set; }
        public decimal OppPointsPerDrive { get; set; }
        public decimal OppThirdFourthRate { get; set; }
        public decimal? OppRzTdRate { get; set; }

        // ST / Discipline
        public decimal NetPunt { get; set; }
        public decimal FgPctShrunk { get; set; }
        public decimal FieldPosDiff { get; set; }
        public decimal TurnoverMarginPerDrive { get; set; }
        public decimal PenaltyYardsPerPlay { get; set; }

        // bookkeeping
        public DateTime ComputedUtc { get; set; }
        public string InputsHash { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionMetric>
        {
            public void Configure(EntityTypeBuilder<CompetitionMetric> b)
            {
                b.ToTable(nameof(CompetitionMetric));
                b.HasKey(x => new { x.CompetitionId, x.FranchiseSeasonId });
                b.HasIndex(x => new { x.Season, x.FranchiseSeasonId });
                foreach (var p in b.Metadata.GetProperties().Where(p => p.ClrType == typeof(decimal)))
                    b.Property(p.Name).HasPrecision(18, 6);
                b.Property(x => x.InputsHash).HasMaxLength(64);
            }
        }
    }

}
