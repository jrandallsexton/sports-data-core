using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;

namespace SportsData.Api.Infrastructure.Data
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
            builder.UseNpgsql();
            return new AppDataContext(builder.Options);
        }
    }
}
