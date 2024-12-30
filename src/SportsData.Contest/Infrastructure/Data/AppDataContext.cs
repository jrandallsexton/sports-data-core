using Microsoft.EntityFrameworkCore;

namespace SportsData.Contest.Infrastructure.Data
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options)
            : base(options) { }
    }
}
