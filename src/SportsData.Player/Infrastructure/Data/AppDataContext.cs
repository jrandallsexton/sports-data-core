using Microsoft.EntityFrameworkCore;

namespace SportsData.Player.Infrastructure.Data
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options)
            : base(options) { }
    }
}
