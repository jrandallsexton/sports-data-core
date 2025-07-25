using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public class AthleteExternalId : ExternalId
    {
        public Guid AthleteId { get; set; }

        public Athlete Athlete { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<AthleteExternalId>
        {
            public void Configure(EntityTypeBuilder<AthleteExternalId> builder)
            {
                builder.ToTable(nameof(AthleteExternalId));
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.Athlete)
                    .WithMany(v => v.ExternalIds)
                    .HasForeignKey(t => t.AthleteId);
            }
        }
    }
}
