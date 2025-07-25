using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public class Location : CanonicalEntityBase<Guid>
    {
        public string? City { get; set; }

        public string? State { get; set; }

        public string? Country { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<Location>
        {
            public void Configure(EntityTypeBuilder<Location> builder)
            {
                builder.ToTable(nameof(Location));

                builder.HasKey(l => l.Id);

                builder.Property(l => l.City)
                    .HasMaxLength(128);

                builder.Property(l => l.State)
                    .HasMaxLength(64);

                builder.Property(l => l.Country)
                    .HasMaxLength(64);
            }
        }
    }

}
