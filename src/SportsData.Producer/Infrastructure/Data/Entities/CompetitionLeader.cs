using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionLeader : CanonicalEntityBase<Guid>
    {
        public Guid CompetitionId { get; set; }

        public Competition Competition { get; set; } = null!;

        public int LeaderCategoryId { get; set; }

        public CompetitionLeaderCategory LeaderCategory { get; set; } = null!;

        public ICollection<CompetitionLeaderStat> Stats { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionLeader>
        {
            public void Configure(EntityTypeBuilder<CompetitionLeader> builder)
            {
                builder.ToTable(nameof(CompetitionLeader));

                builder.HasKey(x => x.Id);

                builder.HasOne(x => x.Competition)
                    .WithMany(x => x.Leaders)
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.LeaderCategory)
                    .WithMany()
                    .HasForeignKey(x => x.LeaderCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }
    }
}