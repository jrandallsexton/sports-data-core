using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseLogo : CanonicalEntityBase<Guid>, ILogo
    {
        public Guid FranchiseId { get; set; }

        public required string OriginalUrlHash { get; set; }

        public required Uri Uri { get; set; }

        public long? Height { get; set; }

        public long? Width { get; set; }

        public List<string>? Rel { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseLogo>
        {
            public void Configure(EntityTypeBuilder<FranchiseLogo> builder)
            {
                builder.ToTable(nameof(FranchiseLogo));
                builder.HasKey(t => t.Id);
                builder.HasOne<Franchise>()
                    .WithMany(x => x.Logos)
                    .HasForeignKey(x => x.FranchiseId);
                builder.HasIndex(x => x.OriginalUrlHash);
                builder.Property(x => x.Uri).HasMaxLength(256);
                builder.Property(x => x.OriginalUrlHash).HasMaxLength(64);
            }
        }
    }
}