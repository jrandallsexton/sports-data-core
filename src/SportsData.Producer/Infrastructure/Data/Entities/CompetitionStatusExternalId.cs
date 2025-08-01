using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionStatusExternalId : ExternalId
    {
        public Guid CompetitionStatusId { get; set; }

        public CompetitionStatus CompetitionStatus { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionStatusExternalId>
        {
            public void Configure(EntityTypeBuilder<CompetitionStatusExternalId> builder)
            {
                builder.ToTable(nameof(CompetitionStatusExternalId));
                builder.HasKey(t => t.Id);

                builder.HasOne(t => t.CompetitionStatus)
                    .WithMany(x => x.ExternalIds)
                    .HasForeignKey(t => t.CompetitionStatusId);
            }
        }
    }
}