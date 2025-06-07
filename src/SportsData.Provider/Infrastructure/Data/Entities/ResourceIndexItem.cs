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

        public string Url { get; set; }

        public DateTime? LastAccessed { get; set; }

        public int Depth { get; set; } = 0;

        public string UrlHash { get; set; }

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

                builder.Property(x => x.UrlHash)
                    .HasMaxLength(64)
                    .IsRequired();

                builder.HasIndex(x => x.UrlHash)
                    .HasDatabaseName("IX_ResourceIndexItem_UrlHash");

                builder.HasIndex(x => new { x.ResourceIndexId, x.UrlHash })
                    .IsUnique()
                    .HasDatabaseName("IX_ResourceIndexItem_Composite");

                builder.HasIndex(x => x.LastAccessed)
                    .HasDatabaseName("IX_ResourceIndexItem_LastAccessed");
            }
        }
    }
}
