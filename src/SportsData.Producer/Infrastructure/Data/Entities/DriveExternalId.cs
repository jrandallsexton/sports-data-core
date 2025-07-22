using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class DriveExternalId : ExternalId
    {
        public Guid DriveId { get; set; }

        public Drive Drive { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<DriveExternalId>
        {
            public void Configure(EntityTypeBuilder<DriveExternalId> builder)
            {
                builder.ToTable(nameof(DriveExternalId));
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.Drive)
                       .WithMany(cd => cd.ExternalIds)
                       .HasForeignKey(t => t.DriveId);
            }
        }
    }
}
