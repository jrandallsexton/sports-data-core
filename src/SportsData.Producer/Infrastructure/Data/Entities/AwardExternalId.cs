using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class AwardExternalId : ExternalId
{
    public Guid AwardId { get; set; }

    public Award Award { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<AwardExternalId>
    {
        public void Configure(EntityTypeBuilder<AwardExternalId> builder)
        {
            builder.ToTable("AwardExternalId");
            builder.HasKey(t => t.Id);
            builder.HasOne(t => t.Award)
                   .WithMany(c => c.ExternalIds)
                   .HasForeignKey(t => t.AwardId);
        } 
    }
}
