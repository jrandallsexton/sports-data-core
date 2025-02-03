using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data.Entities
{
    public class ResourceIndexItem : CanonicalEntityBase<Guid>
    {
        public Guid ResourceIndexId { get; set; }

        public int OriginalUrlHash { get; set; }

        public string Url { get; set; }

        public DateTime? LastAccessed { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<ResourceIndexItem>
        {
            public void Configure(EntityTypeBuilder<ResourceIndexItem> builder)
            {
                builder.ToTable("ResourceIndexItem");
                builder.HasKey(t => t.Id);
                builder.HasOne<ResourceIndex>()
                    .WithMany(x => x.Items)
                    .HasForeignKey(x => x.ResourceIndexId);
                builder.HasIndex(x => x.OriginalUrlHash);
            }
        }

    }
}
