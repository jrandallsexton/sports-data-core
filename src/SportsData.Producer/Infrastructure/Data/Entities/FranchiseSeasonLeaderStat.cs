using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    /// <summary>
    /// Represents an individual athlete's leader statistic for a franchise season category.
    /// Multiple athletes can be leaders in the same category (e.g., top 3 rushers).
    /// </summary>
    public class FranchiseSeasonLeaderStat : CanonicalEntityBase<Guid>
    {
        public Guid FranchiseSeasonLeaderId { get; set; }
        public FranchiseSeasonLeader FranchiseSeasonLeader { get; set; } = null!;

        public Guid AthleteSeasonId { get; set; }
        public AthleteSeason AthleteSeason { get; set; } = null!;

        public required string DisplayValue { get; set; }
        public decimal Value { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonLeaderStat>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonLeaderStat> builder)
            {
                builder.ToTable(nameof(FranchiseSeasonLeaderStat));

                builder.HasKey(x => x.Id);

                builder.Property(x => x.DisplayValue)
                    .HasMaxLength(64);

                builder.Property(x => x.Value)
                    .HasPrecision(18, 6);

                builder.HasOne(x => x.FranchiseSeasonLeader)
                    .WithMany(x => x.Stats)
                    .HasForeignKey(x => x.FranchiseSeasonLeaderId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.AthleteSeason)
                    .WithMany()
                    .HasForeignKey(x => x.AthleteSeasonId)
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }
    }
}
