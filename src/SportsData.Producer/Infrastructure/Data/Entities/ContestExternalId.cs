using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class ContestExternalId : ExternalId
    {
        public Contest Contest { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<ContestExternalId>
        {
            public void Configure(EntityTypeBuilder<ContestExternalId> builder)
            {
                builder.ToTable("ContestExternalId");
                builder.HasKey(t => t.Id);
            }
        }
    }
}
