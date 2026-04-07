using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Extensions;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Baseball
{
    public class BaseballDataContext(DbContextOptions<BaseballDataContext> options) :
        TeamSportDataContext(options)
    {
        public new DbSet<BaseballAthlete> Athletes { get; set; }

        public DbSet<AthleteSeasonHotZone> AthleteSeasonHotZones { get; set; }

        public DbSet<AthleteSeasonHotZoneEntry> AthleteSeasonHotZoneEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.WithUriConverter();
            modelBuilder.ApplyConfiguration(new Athlete.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new BaseballAthlete.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteSeasonHotZone.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteSeasonHotZoneEntry.EntityConfiguration());
        }
    }
}
