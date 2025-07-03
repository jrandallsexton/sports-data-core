using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeasonAward : CanonicalEntityBase<Guid>
    {
        public Guid FranchiseSeasonId { get; set; }
        public FranchiseSeason FranchiseSeason { get; set; } = null!;

        public Guid AwardId { get; set; }
        public Award Award { get; set; } = null!;

        public ICollection<FranchiseSeasonAwardWinner> Winners { get; set; } = new List<FranchiseSeasonAwardWinner>();

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonAward>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonAward> builder)
            {
                builder.ToTable("FranchiseSeasonAward");
                builder.HasKey(x => x.Id);
                builder.HasOne(x => x.FranchiseSeason)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseSeasonId);
                builder.HasOne(x => x.Award)
                    .WithMany(x => x.FranchiseSeasonAwards)
                    .HasForeignKey(x => x.AwardId);
            }
        }
    }
}
