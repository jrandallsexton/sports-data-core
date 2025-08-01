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

        public DbSet<CompetitionLeaderStat> CompetitionLeaderStats { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.WithUriConverter();
            modelBuilder.ApplyConfiguration(new Athlete.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteSeason.EntityConfiguration());
        }
    }
}