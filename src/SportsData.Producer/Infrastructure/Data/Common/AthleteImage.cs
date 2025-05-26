using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public class AthleteImage : CanonicalEntityBase<Guid>, ILogo
    {
        public Guid AthleteId { get; set; }

        public string OriginalUrlHash { get; set; }

        public required string Url { get; set; }

        public long? Height { get; set; }

        public long? Width { get; set; }

        public List<string>? Rel { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<AthleteImage>
        {
            public void Configure(EntityTypeBuilder<AthleteImage> builder)
            {
                builder.ToTable("AthleteImage");
                builder.HasKey(t => t.Id);
                builder.HasOne<Athlete>()
                    .WithMany(x => x.Images)
                    .HasForeignKey(x => x.AthleteId);
                builder.HasIndex(x => x.OriginalUrlHash);
                builder.Property(x => x.OriginalUrlHash).HasMaxLength(64);
            }
        }
    }
}
