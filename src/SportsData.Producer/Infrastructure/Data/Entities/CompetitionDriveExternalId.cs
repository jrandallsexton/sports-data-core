using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionDriveExternalId : ExternalId
    {
        public Guid DriveId { get; set; }

        public CompetitionDrive Drive { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionDriveExternalId>
        {
            public void Configure(EntityTypeBuilder<CompetitionDriveExternalId> builder)
            {
                builder.ToTable(nameof(CompetitionDriveExternalId));
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.Drive)
                       .WithMany(cd => cd.ExternalIds)
                       .HasForeignKey(t => t.DriveId);
            }
        }
    }
}
