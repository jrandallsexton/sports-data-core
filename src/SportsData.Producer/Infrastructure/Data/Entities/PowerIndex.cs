using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class PowerIndex : CanonicalEntityBase<Guid>
    {
        public required string Name { get; set; }

        public required string DisplayName { get; set; }

        public required string Description { get; set; }

        public required string Abbreviation { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<PowerIndex>
        {
            public void Configure(EntityTypeBuilder<PowerIndex> builder)
            {
                builder.ToTable(nameof(PowerIndex));

                builder.HasKey(x => x.Id);

                builder.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(x => x.DisplayName)
                    .IsRequired()
                    .HasMaxLength(75);

                builder.Property(x => x.Description)
                    .IsRequired()
                    .HasMaxLength(256);

                builder.Property(x => x.Abbreviation)
                    .IsRequired()
                    .HasMaxLength(20);
            }
        }
    }
}