using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionProbabilityExternalId : ExternalId
    {
        public Guid CompetitionProbabilityId { get; set; }

        public CompetitionProbability CompetitionProbability { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionProbabilityExternalId>
        {
            public void Configure(EntityTypeBuilder<CompetitionProbabilityExternalId> builder)
            {
                builder.ToTable(nameof(CompetitionProbabilityExternalId));
                builder.HasKey(t => t.Id);

                builder.HasOne(t => t.CompetitionProbability)
                    .WithMany(p => p.ExternalIds)
                    .HasForeignKey(t => t.CompetitionProbabilityId);
            }
        }
    }
}