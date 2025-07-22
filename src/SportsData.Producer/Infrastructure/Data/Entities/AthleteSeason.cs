using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class AthleteSeason : CanonicalEntityBase<Guid>
    {
        public Guid AthleteId { get; set; }

        public Guid FranchiseSeasonId { get; set; }

        public Guid PositionId { get; set; }

        // TODO: Splits

        public class EntityConfiguration : IEntityTypeConfiguration<AthleteSeason>
        {
            public void Configure(EntityTypeBuilder<AthleteSeason> builder)
            {
                builder.ToTable(nameof(AthleteSeason));
                builder.HasKey(t => t.Id);
                builder.HasOne<Athlete>()
                    .WithMany(x => x.Seasons)
                    .HasForeignKey(x => x.AthleteId);
            }
        }
    }
}
