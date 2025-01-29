using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class PositionExternalId : ExternalId
    {
        public Position Position { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<PositionExternalId>
        {
            public void Configure(EntityTypeBuilder<PositionExternalId> builder)
            {
                builder.ToTable("PositionExternalId");
                builder.HasKey(t => t.Id);
            }
        }
    }
}
