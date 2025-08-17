using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CoachRecordExternalId : ExternalId
    {
        public Guid CoachRecordId { get; set; }

        public CoachRecord Record { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<CoachRecordExternalId>
        {
            public void Configure(EntityTypeBuilder<CoachRecordExternalId> builder)
            {
                builder.ToTable(nameof(CoachRecordExternalId));
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.Record)
                    .WithMany(c => c.ExternalIds)
                    .HasForeignKey(t => t.CoachRecordId);
            }
        }
    }
}
