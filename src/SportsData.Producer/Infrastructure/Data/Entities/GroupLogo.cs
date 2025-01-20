using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class GroupLogo : EntityBase<Guid>, ILogo
    {
        public Guid GroupId { get; set; }

        public string Url { get; set; }

        public long? Height { get; set; }

        public long? Width { get; set; }

        public List<string>? Rel { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<GroupLogo>
        {
            public void Configure(EntityTypeBuilder<GroupLogo> builder)
            {
                builder.ToTable("GroupLogo");
                builder.HasKey(t => t.Id);
                builder.HasOne<Group>()
                    .WithMany(x => x.Logos)
                    .HasForeignKey(x => x.GroupId);
            }
        }
    }
}
