using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionOddsExternalId : ExternalId
    {
        public Guid CompetitionOddsId { get; set; }

        public CompetitionOdds CompetitionOdds { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionOddsExternalId>
        {
            public void Configure(EntityTypeBuilder<CompetitionOddsExternalId> builder)
            {
                builder.ToTable(nameof(CompetitionOddsExternalId));
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.CompetitionOdds)
                    .WithMany(cc => cc.ExternalIds)
                    .HasForeignKey(t => t.CompetitionOddsId);
            }
        }
    }
}
