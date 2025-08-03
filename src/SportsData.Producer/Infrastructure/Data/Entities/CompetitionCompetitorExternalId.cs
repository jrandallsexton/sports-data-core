using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionCompetitorExternalId : ExternalId
    {
        public Guid CompetitionCompetitorId { get; set; }

        public CompetitionCompetitor CompetitionCompetitor { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionCompetitorExternalId>
        {
            public void Configure(EntityTypeBuilder<CompetitionCompetitorExternalId> builder)
            {
                builder.ToTable(nameof(CompetitionCompetitorExternalId));
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.CompetitionCompetitor)
                    .WithMany(cc => cc.ExternalIds)
                    .HasForeignKey(t => t.CompetitionCompetitorId);
            }
        }
    }
}