using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities.Metrics
{
    public class CompetitionMetricDistribution : CanonicalEntityBase<Guid>
    {
        public int Season { get; set; }

        public string MetricName { get; set; } = null!;

        public decimal P5 { get; set; }

        public decimal P95 { get; set; }

        public DateTime ComputedUtc { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionMetricDistribution>
        {
            public void Configure(EntityTypeBuilder<CompetitionMetricDistribution> b)
            {
                b.ToTable(nameof(CompetitionMetricDistribution));
                b.HasKey(x => new { x.Season, x.MetricName });
                b.Property(x => x.MetricName).HasMaxLength(128);
                b.Property(x => x.P5).HasPrecision(18, 6);
                b.Property(x => x.P95).HasPrecision(18, 6);
            }
        }
    }
}
