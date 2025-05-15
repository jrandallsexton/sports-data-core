using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SportsData.Provider.Infrastructure.Data
{
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
