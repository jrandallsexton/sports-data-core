using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public class AthleteImage : CanonicalEntityBase<Guid>, ILogo
    {
        public Guid AthleteId { get; set; }

        public Athlete Athlete { get; set; } = null!;

        public required string OriginalUrlHash { get; set; }

        public required Uri Uri { get; set; }

        public long? Height { get; set; }

        public long? Width { get; set; }

        public List<string>? Rel { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<AthleteImage>
        {
            public void Configure(EntityTypeBuilder<AthleteImage> builder)
            {
                builder.ToTable(nameof(AthleteImage));
                builder.HasKey(t => t.Id);

                builder.HasOne(x => x.Athlete)
                    .WithMany(x => x.Images)
                    .HasForeignKey(x => x.AthleteId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasIndex(x => x.OriginalUrlHash);

                builder.Property(x => x.Uri)
                    .HasMaxLength(256);

                builder.Property(x => x.OriginalUrlHash)
                    .HasMaxLength(64);
            }
        }
    }
}