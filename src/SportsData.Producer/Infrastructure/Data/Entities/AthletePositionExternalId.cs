using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class AthletePositionExternalId : ExternalId
{
    public AthletePosition AthletePosition { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<AthletePositionExternalId>
    {
        public void Configure(EntityTypeBuilder<AthletePositionExternalId> builder)
        {
            builder.ToTable("AthletePositionExternalId");
            builder.HasKey(t => t.Id);
        }
    }
}