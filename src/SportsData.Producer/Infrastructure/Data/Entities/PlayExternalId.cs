using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class PlayExternalId : ExternalId
    {
        public Guid PlayId { get; set; }

        public Play Play { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<PlayExternalId>
        {
            public void Configure(EntityTypeBuilder<PlayExternalId> builder)
            {
                builder.ToTable(nameof(PlayExternalId));
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.Play)
                       .WithMany(cp => cp.ExternalIds)
                       .HasForeignKey(t => t.PlayId);
            }
        }
    }
}
