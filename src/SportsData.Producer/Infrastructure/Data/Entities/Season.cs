using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Season : CanonicalEntityBase<Guid>
    {
        public int Year { get; set; }

        public string Name { get; set; }

        public string Abbreviation { get; set; }

        public string Slug { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public List<SeasonExternalId> ExternalIds { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<Season>
        {
            public void Configure(EntityTypeBuilder<Season> builder)
            {
                builder.ToTable("Season");
                builder.HasKey(t => t.Id);
                builder.Property(p => p.Id).ValueGeneratedNever();
            }
        }
    }
}
