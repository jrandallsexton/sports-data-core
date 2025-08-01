using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionPrediction : CanonicalEntityBase<Guid>
    {
        public Guid CompetitionId { get; set; }

        public Guid FranchiseSeasonId { get; set; }

        public bool IsHome { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionPrediction>
        {
            public void Configure(EntityTypeBuilder<CompetitionPrediction> builder)
            {
                builder.ToTable(nameof(CompetitionPrediction));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.CompetitionId).IsRequired();
                builder.Property(x => x.FranchiseSeasonId).IsRequired();
                builder.Property(x => x.IsHome).IsRequired();
                builder.HasIndex(x => new { x.CompetitionId, x.FranchiseSeasonId, x.IsHome })
                    .IsUnique();
            }
        }
    }
}