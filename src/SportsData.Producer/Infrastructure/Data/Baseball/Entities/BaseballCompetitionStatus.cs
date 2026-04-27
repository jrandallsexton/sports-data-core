using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities;

// MLB-specific subclass of CompetitionStatus. Holds the baseball-only
// fields ESPN ships in its status payload — half-inning indicator,
// period prefix ("Top" / "Bot" / "End"), and the at-bat featured-
// athletes collection (winning/losing pitcher post-game; current
// batter/pitcher in-game). Football's status rows live on
// FootballCompetitionStatus and never see these fields, mirroring
// the FootballCompetition / BaseballCompetition split that already
// lives next door.
public class BaseballCompetitionStatus : CompetitionStatusBase
{
    // 1 = top, 2 = bottom (ESPN's encoding).
    public int? HalfInning { get; set; }

    // "Top", "Bot", "End", or null pre-game.
    public string? PeriodPrefix { get; set; }

    public ICollection<BaseballCompetitionStatusFeaturedAthlete> FeaturedAthletes { get; set; } = [];

    public new class EntityConfiguration : IEntityTypeConfiguration<BaseballCompetitionStatus>
    {
        public void Configure(EntityTypeBuilder<BaseballCompetitionStatus> builder)
        {
            builder.Property(x => x.PeriodPrefix).HasMaxLength(10);

            builder.HasMany(x => x.FeaturedAthletes)
                .WithOne(x => x.CompetitionStatus)
                .HasForeignKey(x => x.CompetitionStatusId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
