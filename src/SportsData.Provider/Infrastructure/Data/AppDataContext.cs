using Microsoft.EntityFrameworkCore;
using SportsData.Provider.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options) :
            base(options) { }

        public DbSet<ResourceIndex> Resources { get; set; }
    }
}