using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeasonExternalId : ExternalId
    {
        public FranchiseSeason FranchiseSeason { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonExternalId>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonExternalId> builder)
            {
                builder.ToTable("FranchiseSeasonExternalId");
                builder.HasKey(t => t.Id);
                builder.HasOne<FranchiseSeason>()
                    .WithMany()
                    .HasForeignKey(x => x.Id);
            }
        }
    }
}
