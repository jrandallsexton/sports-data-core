using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data.Entities
{
    public class ResourceIndex : CanonicalEntityBase<Guid>
    {
        public int Ordinal { get; set; }

        public bool IsRecurring { get; set; }

        public bool IsEnabled { get; set; }

        public SourceDataProvider Provider { get; set; }

        public DocumentType DocumentType { get; set; }

        public Sport SportId { get; set; }

        public string Endpoint { get; set; }

        public string EndpointMask { get; set; }

        public bool IsSeasonSpecific { get; set; }

        public int? SeasonYear { get; set; }

        public DateTime? LastAccessed { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<ResourceIndex>
        {
            public void Configure(EntityTypeBuilder<ResourceIndex> builder)
            {
                builder.ToTable("ResourceIndex");
                builder.HasKey(t => t.Id);
            }
        }
    }
}
