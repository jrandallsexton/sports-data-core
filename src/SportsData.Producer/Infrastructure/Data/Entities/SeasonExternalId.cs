using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class SeasonExternalId : ExternalId
    {
        public Season Season { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<SeasonExternalId>
        {
            public void Configure(EntityTypeBuilder<SeasonExternalId> builder)
            {
                builder.ToTable("SeasonExternalId");
                builder.HasKey(t => t.Id);
            }
        }
    }
}
