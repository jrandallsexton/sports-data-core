using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Api.Application;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities;

public class ContestPrediction : CanonicalEntityBase<Guid>
{
    public Guid ContestId { get; set; }

    public Guid WinnerFranchiseSeasonId { get; set; }

    public decimal WinProbability { get; set; } = 0;

    public PickType PredictionType { get; set; }

    public string ModelVersion { get; set; } = default!; // e.g. "v1.0.0" or "MetricBot-Oct2025"

    public class EntityConfiguration : IEntityTypeConfiguration<ContestPrediction>
    {
        public void Configure(EntityTypeBuilder<ContestPrediction> builder)
        {
            builder.ToTable(nameof(ContestPrediction));

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ContestId)
                .IsRequired();

            builder.Property(x => x.WinnerFranchiseSeasonId)
                .IsRequired();

            builder.Property(x => x.WinProbability)
                .HasPrecision(5, 4)
                .IsRequired();

            builder.Property(x => x.PredictionType)
                .HasConversion<int>() // Store enum as integer
                .IsRequired();

            builder.Property(x => x.ModelVersion)
                .HasMaxLength(64)
                .IsRequired();

            builder.HasIndex(x => x.ModelVersion);
            builder.HasIndex(x => new { x.ContestId, x.PredictionType, x.ModelVersion })
                .IsUnique();
        }
    }
}