using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class VenueExternalId : ExternalId
{
    public Venue Venue { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<VenueExternalId>
    {
        public void Configure(EntityTypeBuilder<VenueExternalId> builder)
        {
            builder.ToTable("VenueExternalId");
            builder.HasKey(t => t.Id);
        }
    }

}