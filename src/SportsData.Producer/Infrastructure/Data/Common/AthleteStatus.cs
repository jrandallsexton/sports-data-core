using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public class AthleteStatus : CanonicalEntityBase<Guid>
    {
        public string ExternalId { get; set; } = default!;

        public string? Abbreviation { get; set; }

        public string? Name { get; set; }

        public string? Type { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<AthleteStatus>
        {
            public void Configure(EntityTypeBuilder<AthleteStatus> builder)
            {
                builder.ToTable("AthleteStatus");

                builder.HasKey(s => s.Id);

                builder.Property(s => s.ExternalId)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(s => s.Abbreviation)
                    .HasMaxLength(50);

                builder.Property(s => s.Name)
                    .HasMaxLength(100);

                builder.Property(s => s.Type)
                    .HasMaxLength(50);
            }
        }
    }
}