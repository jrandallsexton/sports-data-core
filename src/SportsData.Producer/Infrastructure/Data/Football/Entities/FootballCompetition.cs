using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Football.Entities
{
    public class FootballCompetition : CompetitionBase
    {
        public ICollection<FootballCompetitionPlay> Plays { get; set; } = [];

        public ICollection<CompetitionDrive> Drives { get; set; } = [];

        public new class EntityConfiguration : IEntityTypeConfiguration<FootballCompetition>
        {
            public void Configure(EntityTypeBuilder<FootballCompetition> builder)
            {
                builder.HasOne<FootballContest>()
                    .WithMany(x => x.Competitions)
                    .HasForeignKey(x => x.ContestId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.Plays)
                    .WithOne()
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.Drives)
                    .WithOne()
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
