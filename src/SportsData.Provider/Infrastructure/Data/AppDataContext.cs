using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using SportsData.Provider.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options) :
            base(options) { }

        public DbSet<ResourceIndex> Resources { get; set; }

        public DbSet<ResourceIndexItem> ResourceIndexItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ResourceIndex.EntityConfiguration).Assembly);
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