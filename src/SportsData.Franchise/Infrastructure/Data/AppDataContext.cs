using Microsoft.EntityFrameworkCore;

namespace SportsData.Franchise.Infrastructure.Data
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options)
            : base(options) { }
    }
}
