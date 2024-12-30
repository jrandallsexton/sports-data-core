using Microsoft.EntityFrameworkCore;

namespace SportsData.Season.Data;

public class AppDataContext : DbContext
{
    public AppDataContext(DbContextOptions<AppDataContext> options)
        : base(options) { }
}