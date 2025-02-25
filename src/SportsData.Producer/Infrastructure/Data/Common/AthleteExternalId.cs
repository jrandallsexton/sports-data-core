using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Common
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
