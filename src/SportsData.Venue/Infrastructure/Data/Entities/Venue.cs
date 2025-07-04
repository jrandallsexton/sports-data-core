using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Venue.Infrastructure.Data.Entities
{
    public class Venue : EntityBase<int>
    {
        public string Name { get; set; }

        public string ShortName { get; set; }

        public bool IsGrass { get; set; }

        public bool IsIndoor { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<Venue>
        {
            public void Configure(EntityTypeBuilder<Venue> builder)
            {
                // Table name
                builder.ToTable("Venue");

                // Primary Key
                builder.HasKey(v => v.Id);

                // Name - Required, reasonable length limit
                builder.Property(v => v.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                // ShortName - Optional or Required? Assuming Required
                builder.Property(v => v.ShortName)
                    .IsRequired()
                    .HasMaxLength(100);

                // Booleans - EF Core maps these automatically, no extra config needed
                builder.Property(v => v.IsGrass).IsRequired();
                builder.Property(v => v.IsIndoor).IsRequired();

                // Optional: add a unique index on ShortName if that should be unique
                // builder.HasIndex(v => v.ShortName).IsUnique();
            }
        }

    }
}
