using Microsoft.EntityFrameworkCore;

namespace SportsData.Notification.Infrastructure.Data
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options)
            : base(options) { }
    }
}
