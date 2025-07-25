using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class ContestExternalId : ExternalId
    {
        public Guid ContestId { get; set; }

        public Contest Contest { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<ContestExternalId>
        {
            public void Configure(EntityTypeBuilder<ContestExternalId> builder)
            {
                builder.ToTable(nameof(ContestExternalId));
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.Contest)
                    .WithMany(c => c.ExternalIds)
                    .HasForeignKey(k => k.ContestId);
            }
        }
    }
}
