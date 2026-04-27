using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities
{
    public class BaseballCompetition : CompetitionBase
    {
        public ICollection<BaseballCompetitionPlay> Plays { get; set; } = [];

        // Sport-specific Status nav typed to the MLB subclass so
        // HalfInning / PeriodPrefix / FeaturedAthletes are reachable
        // without an OfType cast.
        public BaseballCompetitionStatus? Status { get; set; }

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

                builder.HasOne(x => x.Status)
                    .WithOne()
                    .HasForeignKey<BaseballCompetitionStatus>(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
