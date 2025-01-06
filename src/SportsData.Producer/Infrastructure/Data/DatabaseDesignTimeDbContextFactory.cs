using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SportsData.Producer.Infrastructure.Data
{
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
