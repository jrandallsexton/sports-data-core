using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SportsData.Core.Infrastructure.Data.Extensions;
using SportsData.Provider.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options) :
            base(options) { }

        public DbSet<ResourceIndex> ResourceIndexJobs { get; set; }

        public DbSet<ResourceIndexItem> ResourceIndexItems { get; set; }

        public DbSet<ScheduledJob> ScheduledJobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.WithUriConverter();
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ResourceIndex).Assembly);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ConfigureWarnings(x =>
            {
                x.Ignore([RelationalEventId.PendingModelChangesWarning]);
            });
            optionsBuilder.EnableSensitiveDataLogging(false);
        }
    }
}