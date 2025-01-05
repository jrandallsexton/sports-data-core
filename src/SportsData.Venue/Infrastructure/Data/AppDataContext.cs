using Microsoft.EntityFrameworkCore;

namespace SportsData.Venue.Infrastructure.Data
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options) :
            base(options)
        { }

        public DbSet<Entities.Venue> Venues { get; set; }
    }
}
