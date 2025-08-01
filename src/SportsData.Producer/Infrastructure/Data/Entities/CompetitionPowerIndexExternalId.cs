using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionPowerIndexExternalId : ExternalId
    {
        public Guid CompetitionPowerIndexId { get; set; }

        public CompetitionPowerIndex CompetitionPowerIndex { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionPowerIndexExternalId>
        {
            public void Configure(EntityTypeBuilder<CompetitionPowerIndexExternalId> builder)
            {
                builder.ToTable(nameof(CompetitionPowerIndexExternalId));
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.CompetitionPowerIndex)
                    .WithMany(v => v.ExternalIds)
                    .HasForeignKey(t => t.CompetitionPowerIndexId);
            }
        }
    }
}
