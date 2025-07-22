using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeasonLogo : CanonicalEntityBase<Guid>, ILogo
    {
        public Guid FranchiseSeasonId { get; set; }

        public required string OriginalUrlHash { get; set; }

        public required Uri Uri { get; set; }

        public long? Height { get; set; }

        public long? Width { get; set; }

        public List<string>? Rel { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonLogo>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonLogo> builder)
            {
                builder.ToTable(nameof(FranchiseSeasonLogo));
                builder.HasKey(t => t.Id);
                builder.HasOne<FranchiseSeason>()
                    .WithMany(x => x.Logos)
                    .HasForeignKey(x => x.FranchiseSeasonId);
                builder.HasIndex(x => x.OriginalUrlHash);
                builder.Property(x => x.Uri).HasMaxLength(256);
                builder.Property(x => x.OriginalUrlHash).HasMaxLength(64);
            }
        }
    }
}
