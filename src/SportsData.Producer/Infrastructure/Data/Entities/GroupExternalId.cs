using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class GroupExternalId : ExternalId
{
    public Group Group { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<GroupExternalId>
    {
        public void Configure(EntityTypeBuilder<GroupExternalId> builder)
        {
            builder.ToTable("GroupExternalId");
            builder.HasKey(t => t.Id);
        }
    }
}