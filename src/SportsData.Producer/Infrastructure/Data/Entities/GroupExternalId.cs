using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class GroupExternalId : ExternalId
{
    public Guid GroupId { get; set; }

    public Group Group { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<GroupExternalId>
    {
        public void Configure(EntityTypeBuilder<GroupExternalId> builder)
        {
            builder.ToTable(nameof(GroupExternalId));
            builder.HasKey(t => t.Id);
            builder.HasOne(t => t.Group)
                   .WithMany(g => g.ExternalIds)
                   .HasForeignKey(t => t.GroupId);
        }
    }
}