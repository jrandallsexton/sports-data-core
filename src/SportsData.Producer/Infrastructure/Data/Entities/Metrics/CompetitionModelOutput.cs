using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities.Metrics
{
    public class CompetitionModelOutput : CanonicalEntityBase<Guid>
    {
        public Guid ModelRunId { get; set; }

        public Guid CompetitionId { get; set; }

        public Guid FranchiseSeasonId { get; set; }

        public int Season { get; set; }

        public decimal OffenseScore { get; set; }

        public decimal DefenseScore { get; set; }

        public decimal SpecialTeamsScore { get; set; }

        public decimal DisciplineScore { get; set; }

        public decimal TotalScore { get; set; }

        public string? ExplainerJson { get; set; }

        public DateTime ComputedUtc { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionModelOutput>
        {
            public void Configure(EntityTypeBuilder<CompetitionModelOutput> b)
            {
                b.ToTable(nameof(CompetitionModelOutput));
                b.HasKey(x => new { x.ModelRunId, x.CompetitionId, x.FranchiseSeasonId });
                foreach (var p in b.Metadata.GetProperties().Where(p => p.ClrType == typeof(decimal)))
                    b.Property(p.Name).HasPrecision(18, 6);
                b.HasIndex(x => new { x.Season, x.FranchiseSeasonId });
            }
        }
    }
}
