using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionCompetitorScoreExternalId : ExternalId
    {
        public Guid CompetitionCompetitorScoreId { get; set; }

        public CompetitionCompetitorScore CompetitionCompetitorScore { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionCompetitorScoreExternalId>
        {
            public void Configure(EntityTypeBuilder<CompetitionCompetitorScoreExternalId> builder)
            {
                builder.ToTable(nameof(CompetitionCompetitorScoreExternalId));
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.CompetitionCompetitorScore)
                    .WithMany(t => t.ExternalIds)
                    .HasForeignKey(t => t.CompetitionCompetitorScoreId);
            }
        }
    }
}