using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data.Entities
{
    public class ResourceIndexItem : CanonicalEntityBase<Guid>, IHasSourceUrlHash
    {
        public Guid ResourceIndexId { get; set; }

        public Guid? ParentItemId { get; set; }

        public required Uri Uri { get; set; }

        public DateTime? LastAccessed { get; set; }

        public int Depth { get; set; } = 0;

        public required string SourceUrlHash { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<ResourceIndexItem>
        {
            public void Configure(EntityTypeBuilder<ResourceIndexItem> builder)
            {
                builder.ToTable("ResourceIndexItem");

                builder.HasKey(t => t.Id);

                builder.HasOne<ResourceIndex>()
                    .WithMany(x => x.Items)
                    .HasForeignKey(x => x.ResourceIndexId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.Property(p => p.Uri)
                    .HasMaxLength(255);

                builder.Property(x => x.SourceUrlHash)
                    .HasMaxLength(64)
                    .IsRequired();

                builder.HasIndex(x => x.SourceUrlHash)
                    .HasDatabaseName("IX_ResourceIndexItem_UrlHash");

                builder.HasIndex(x => new { x.ResourceIndexId, UrlHash = x.SourceUrlHash })
                    .IsUnique()
                    .HasDatabaseName("IX_ResourceIndexItem_Composite");

                builder.HasIndex(x => x.LastAccessed)
                    .HasDatabaseName("IX_ResourceIndexItem_LastAccessed");
            }
        }
    }
}
