using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class GroupSeason : EntityBase<Guid>
    {
        public int Season { get; set; }

        public Guid GroupId { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<GroupSeason>
        {
            public void Configure(EntityTypeBuilder<GroupSeason> builder)
            {
                builder.ToTable("GroupSeason");
                builder.HasKey(t => t.Id);
                builder.HasOne<Group>()
                    .WithMany(x => x.Seasons)
                    .HasForeignKey(x => x.GroupId);
            }
        }
    }
}
