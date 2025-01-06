using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SportsData.Venue.Infrastructure.Data
{
    /// <summary>
    /// https://khalidabuhakmeh.com/fix-unable-to-resolve-dbcontextoptions-for-ef-core
    /// </summary>
    public class DatabaseDesignTimeDbContextFactory
        : IDesignTimeDbContextFactory<AppDataContext>
    {
        public AppDataContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<AppDataContext>();
            builder.UseSqlServer();
            return new AppDataContext(builder.Options);
        }
    }
}
