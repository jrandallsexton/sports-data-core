using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class AthletePositionExternalId : ExternalId
{
    public Guid AthletePositionId { get; set; }

    public AthletePosition AthletePosition { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<AthletePositionExternalId>
    {
        public void Configure(EntityTypeBuilder<AthletePositionExternalId> builder)
        {
            builder.ToTable("AthletePositionExternalId");
            builder.HasKey(t => t.Id);

            builder.HasOne(e => e.AthletePosition)
                .WithMany(p => p.ExternalIds)
                .HasForeignKey(e => e.AthletePositionId);
        }
    }
}