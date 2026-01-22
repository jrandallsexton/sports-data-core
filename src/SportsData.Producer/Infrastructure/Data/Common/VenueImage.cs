using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public class VenueImage : CanonicalEntityBase<Guid>, ILogo
    {
        public Guid VenueId {  get; set; }

        public required string OriginalUrlHash { get; set; }

        public required Uri Uri { get; set; }

        public long? Height { get; set; }

        public long? Width { get; set; }

        public List<string>? Rel { get; set; }

        public bool? IsForDarkBg { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<VenueImage>
        {
            public void Configure(EntityTypeBuilder<VenueImage> builder)
            {
                builder.ToTable(nameof(VenueImage));
                builder.HasKey(t => t.Id);
                builder.HasOne<Venue>()
                    .WithMany(x => x.Images)
                    .HasForeignKey(x => x.VenueId);
                builder.HasIndex(x => x.OriginalUrlHash);
                builder.Property(x => x.OriginalUrlHash).HasMaxLength(64);
                builder.Property(x => x.Uri).HasMaxLength(256);
            }
        }
    }
}
