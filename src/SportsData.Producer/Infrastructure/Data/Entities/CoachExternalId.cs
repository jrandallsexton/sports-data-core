using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class CoachExternalId : ExternalId
{
    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<CoachExternalId>
    {
        public void Configure(EntityTypeBuilder<CoachExternalId> builder)
        {
            builder.ToTable("CoachExternalId");
            builder.HasKey(t => t.Id);
            builder.HasOne(t => t.Coach)
                   .WithMany(c => c.ExternalIds)
                   .HasForeignKey(t => t.CoachId);
        }
    }
}
