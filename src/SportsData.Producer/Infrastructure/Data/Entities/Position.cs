using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Position : CanonicalEntityBase<Guid>
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Abbrevation { get; set; }

        public bool IsLeaf { get; set; }

        public List<PositionExternalId> ExternalIds { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<Position>
        {
            public void Configure(EntityTypeBuilder<Position> builder)
            {
                builder.ToTable("Position");
                builder.HasKey(t => t.Id);
            }
        }
    }
}
