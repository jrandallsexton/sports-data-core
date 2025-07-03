using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Venue : CanonicalEntityBase<Guid>, IHasSlug, IHasExternalIds
    {
        public required string Name { get; set; }

        public string? ShortName { get; set; }

        public bool IsGrass { get; set; }

        public bool IsIndoor { get; set; }

        public required string Slug { get; set; }

        public int Capacity { get; set; }

        public required string City { get; set; }

        public required string State { get; set; }

        public required string PostalCode { get; set; }

        public string Country { get; set; } = "US";

        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        public List<VenueExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public List<VenueImage> Images { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<Venue>
        {
            public void Configure(EntityTypeBuilder<Venue> builder)
            {
                builder.ToTable("Venue");
                builder.HasKey(t => t.Id);
                builder.Property(p => p.Id).ValueGeneratedNever();
                builder.Property(p => p.Country).HasMaxLength(20);
                builder.Property(p => p.Name).HasMaxLength(75);
                builder.Property(p => p.ShortName).HasMaxLength(75);
                builder.Property(p => p.Slug).HasMaxLength(75);
                builder.Property(p => p.PostalCode).HasMaxLength(20);
                builder.Property(p => p.State).HasMaxLength(20);
                builder.Property(p => p.City).HasMaxLength(25);
            }
        }
    }
}
