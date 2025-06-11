using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class GroupSeasonLogo : CanonicalEntityBase<Guid>, ILogo
    {
        public Guid GroupSeasonId { get; set; }

        public required string OriginalUrlHash { get; set; }

        public required Uri Uri { get; set; }

        public long? Height { get; set; }

        public long? Width { get; set; }

        public List<string>? Rel { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<GroupSeasonLogo>
        {
            public void Configure(EntityTypeBuilder<GroupSeasonLogo> builder)
            {
                builder.ToTable("GroupSeasonLogo");
                builder.HasKey(t => t.Id);
                builder.HasOne<GroupSeason>()
                    .WithMany(x => x.Logos)
                    .HasForeignKey(x => x.GroupSeasonId);
                builder.HasIndex(x => x.OriginalUrlHash);
                builder.Property(x => x.Uri).HasMaxLength(256);
                builder.Property(x => x.OriginalUrlHash).HasMaxLength(64);
            }
        }
    }
}
