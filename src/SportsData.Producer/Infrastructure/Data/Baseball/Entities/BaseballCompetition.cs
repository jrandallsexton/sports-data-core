using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities
{
    public class BaseballCompetition : Competition
    {
        public ICollection<BaseballCompetitionPlay> Plays { get; set; } = [];

        public new class EntityConfiguration : IEntityTypeConfiguration<BaseballCompetition>
        {
            public void Configure(EntityTypeBuilder<BaseballCompetition> builder)
            {
                builder.HasOne<BaseballContest>()
                    .WithMany(x => x.Competitions)
                    .HasForeignKey(x => x.ContestId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.Plays)
                    .WithOne()
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
