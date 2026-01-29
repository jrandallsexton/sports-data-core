using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    /// <summary>
    /// Represents a leader category for a franchise season (e.g., passing leader, rushing leader).
    /// Contains the rollup of leader statistics for the season.
    /// </summary>
    public class FranchiseSeasonLeader : CanonicalEntityBase<Guid>
    {
        public Guid FranchiseSeasonId { get; set; }

        public FranchiseSeason FranchiseSeason { get; set; } = null!;

        public int LeaderCategoryId { get; set; }

        public CompetitionLeaderCategory LeaderCategory { get; set; } = null!;

        public ICollection<FranchiseSeasonLeaderStat> Stats { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonLeader>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonLeader> builder)
            {
                builder.ToTable(nameof(FranchiseSeasonLeader));

                builder.HasKey(x => x.Id);

                builder.HasOne(x => x.FranchiseSeason)
                    .WithMany(x => x.Leaders)
                    .HasForeignKey(x => x.FranchiseSeasonId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.LeaderCategory)
                    .WithMany()
                    .HasForeignKey(x => x.LeaderCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }
    }
}
