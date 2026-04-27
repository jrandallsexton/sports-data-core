using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Extensions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Infrastructure.Data.Football
{
    public class FootballDataContext(DbContextOptions<FootballDataContext> options) :
        TeamSportDataContext(options)
    {
        public new DbSet<FootballAthlete> Athletes { get; set; }

        public new DbSet<FootballAthleteSeason> AthleteSeasons { get; set; }

        public new DbSet<FootballContest> Contests { get; set; }

        public new DbSet<FootballCompetition> Competitions { get; set; }

        public new DbSet<FootballCompetitionPlay> CompetitionPlays { get; set; }

        // Sport-specific status DbSet — typed entry point for the
        // football side of the CompetitionStatus split.
        public DbSet<FootballCompetitionStatus> FootballCompetitionStatuses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.WithUriConverter();
            modelBuilder.ApplyConfiguration(new AthleteBase.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FootballCompetition.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FootballCompetitionPlay.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FootballCompetitionStatus.EntityConfiguration());
        }
    }
}
