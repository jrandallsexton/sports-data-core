using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Extensions;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball
{
    public class BaseballDataContext(DbContextOptions<BaseballDataContext> options) :
        TeamSportDataContext(options)
    {
        public new DbSet<BaseballAthlete> Athletes { get; set; }

        public new DbSet<BaseballAthleteSeason> AthleteSeasons { get; set; }

        public new DbSet<BaseballContest> Contests { get; set; }

        public new DbSet<BaseballCompetition> Competitions { get; set; }

        public new DbSet<BaseballCompetitionPlay> CompetitionPlays { get; set; }

        // Sport-specific subclass set: the inherited DbSet<CompetitionStatus>
        // on TeamSportDataContext can still serve plain reads via the base
        // type, but eager-loading FeaturedAthletes / accessing HalfInning /
        // PeriodPrefix requires this typed entry point.
        public DbSet<BaseballCompetitionStatus> BaseballCompetitionStatuses { get; set; }

        public DbSet<BaseballCompetitionStatusFeaturedAthlete> BaseballCompetitionStatusFeaturedAthletes { get; set; }

        public DbSet<AthleteSeasonHotZone> AthleteSeasonHotZones { get; set; }

        public DbSet<AthleteSeasonHotZoneEntry> AthleteSeasonHotZoneEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.WithUriConverter();
            modelBuilder.ApplyConfiguration(new AthleteBase.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new BaseballAthlete.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new BaseballCompetition.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new BaseballCompetitionPlay.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new BaseballCompetitionStatus.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new BaseballCompetitionStatusFeaturedAthlete.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteSeasonHotZone.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteSeasonHotZoneEntry.EntityConfiguration());
        }
    }
}
