using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseExternalId : ExternalId { }

    public class GroupExternalId : ExternalId
    {
        public Group Group { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<GroupExternalId>
        {
            public void Configure(EntityTypeBuilder<GroupExternalId> builder)
            {
                builder.ToTable("GroupExternalId");
                builder.HasKey(t => t.Id);
            }
        }
    }

    public class VenueExternalId : ExternalId { }

    public class ExternalId : EntityBase<Guid>
    {
        public string Value { get; set; }

        public SourceDataProvider Provider { get; set; }
    }
}
