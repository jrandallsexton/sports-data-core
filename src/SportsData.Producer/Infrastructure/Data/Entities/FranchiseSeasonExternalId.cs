using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeasonExternalId : ExternalId
    {
        public Guid FranchiseSeasonId { get; set; }

        public FranchiseSeason FranchiseSeason { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonExternalId>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonExternalId> builder)
            {
                builder.ToTable(nameof(FranchiseSeasonExternalId));

                builder.HasKey(t => t.Id);

                builder.HasOne( t => t.FranchiseSeason)
                    .WithMany(v => v.ExternalIds)
                    .HasForeignKey(x => x.FranchiseSeasonId);
            }
        }
    }
}
