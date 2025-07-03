using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class VenueExternalId : ExternalId
{
    public Guid VenueId { get; set; }

    public Venue Venue { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<VenueExternalId>
    {
        public void Configure(EntityTypeBuilder<VenueExternalId> builder)
        {
            builder.ToTable("VenueExternalId");
            builder.HasKey(t => t.Id);
            builder.HasOne(t => t.Venue)
                   .WithMany(v => v.ExternalIds)
                   .HasForeignKey(t => t.VenueId);
        }
    }
}