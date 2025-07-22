using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionExternalId : ExternalId
    {
        public Guid CompetitionId { get; set; }

        public Competition Competition { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionExternalId>
        {
            public void Configure(EntityTypeBuilder<CompetitionExternalId> builder)
            {
                builder.ToTable(nameof(CompetitionExternalId));
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.Competition)
                       .WithMany(cc => cc.ExternalIds)
                       .HasForeignKey(t => t.CompetitionId);
            }
        }
    }
}
