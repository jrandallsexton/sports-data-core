using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Season : CanonicalEntityBase<Guid>
    {
        public int Year { get; set; }

        public required string Name { get; set; }

        public required string Abbreviation { get; set; }

        public required string Slug { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public List<SeasonExternalId> ExternalIds { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<Season>
        {
            public void Configure(EntityTypeBuilder<Season> builder)
            {
                builder.ToTable("SeasonYear");
                builder.HasKey(t => t.Id);
                builder.Property(p => p.Id).ValueGeneratedNever();
                builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
                builder.Property(t => t.Abbreviation).HasMaxLength(10).IsRequired();
                builder.Property(t => t.Slug).HasMaxLength(10).IsRequired();
            }
        }
    }
}
