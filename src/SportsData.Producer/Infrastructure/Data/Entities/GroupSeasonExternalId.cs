using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class GroupSeasonExternalId : ExternalId
    {
        public Guid GroupSeasonId { get; set; }

        public GroupSeason GroupSeason { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<GroupSeasonExternalId>
        {
            public void Configure(EntityTypeBuilder<GroupSeasonExternalId> builder)
            {
                builder.ToTable(nameof(GroupSeasonExternalId));
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.GroupSeason)
                    .WithMany(x => x.ExternalIds)
                    .HasForeignKey(x => x.GroupSeasonId);
            }
        }
    }
}
