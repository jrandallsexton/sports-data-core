using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class AthleteExternalId : ExternalId
    {
        public Athlete Athlete { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<AthleteExternalId>
        {
            public void Configure(EntityTypeBuilder<AthleteExternalId> builder)
            {
                builder.ToTable("AthleteExternalId");
                builder.HasKey(t => t.Id);
            }
        }
    }
}
