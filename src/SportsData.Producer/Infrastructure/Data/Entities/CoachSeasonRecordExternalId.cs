using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CoachSeasonRecordExternalId : ExternalId
    {
        public Guid CoachSeasonRecordId { get; set; }

        public CoachSeasonRecord Record { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CoachSeasonRecordExternalId>
        {
            public void Configure(EntityTypeBuilder<CoachSeasonRecordExternalId> builder)
            {
                builder.ToTable(nameof(CoachSeasonRecordExternalId));
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.Record)
                    .WithMany(c => c.ExternalIds)
                    .HasForeignKey(t => t.CoachSeasonRecordId);
            }
        }
    }
}
