using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionPredictionValue : CanonicalEntityBase<Guid>
    {
        public Guid CompetitionPredictionId { get; set; }
        public Guid PredictionMetricId { get; set; }

        public decimal? Value { get; set; }
        public string DisplayValue { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionPredictionValue>
        {
            public void Configure(EntityTypeBuilder<CompetitionPredictionValue> builder)
            {
                builder.ToTable(nameof(CompetitionPredictionValue));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.CompetitionPredictionId).IsRequired();
                builder.Property(x => x.PredictionMetricId).IsRequired();
                builder.Property(x => x.Value).HasPrecision(18, 6);
                builder.Property(x => x.DisplayValue).IsRequired().HasMaxLength(50);

                builder.HasIndex(x => new { x.CompetitionPredictionId, x.PredictionMetricId })
                    .IsUnique(); // One stat per prediction per metric
            }
        }
    }
}