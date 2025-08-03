using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionCompetitorLineScoreExternalId : ExternalId
    {
        public Guid CompetitionCompetitorLineScoreId { get; set; }

        public CompetitionCompetitorLineScore CompetitionCompetitorLineScore { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionCompetitorLineScoreExternalId>
        {
            public void Configure(EntityTypeBuilder<CompetitionCompetitorLineScoreExternalId> builder)
            {
                builder.ToTable(nameof(CompetitionCompetitorLineScoreExternalId));
                builder.HasKey(x => x.Id);

                builder.HasOne(x => x.CompetitionCompetitorLineScore)
                    .WithMany(x => x.ExternalIds)
                    .HasForeignKey(x => x.CompetitionCompetitorLineScoreId);
            }
        }
    }
}