using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class AthleteSeasonExternalId : ExternalId
    {
        public Guid AthleteSeasonId { get; set; }

        public AthleteSeason AthleteSeason { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<AthleteSeasonExternalId>
        {
            public void Configure(EntityTypeBuilder<AthleteSeasonExternalId> builder)
            {
                builder.ToTable(nameof(AthleteSeasonExternalId));

                builder.HasKey(t => t.Id);

                builder.HasOne(t => t.AthleteSeason)
                    .WithMany(v => v.ExternalIds)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
