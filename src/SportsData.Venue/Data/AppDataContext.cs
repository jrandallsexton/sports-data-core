using Microsoft.EntityFrameworkCore;

namespace SportsData.Venue.Data
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options) :
            base(options) { }
    }
}
