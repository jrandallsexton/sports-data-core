using Microsoft.EntityFrameworkCore;

namespace SportsData.Provider.Infrastructure.Data
{
    public class ProviderDataContext : DbContext
    {
        public ProviderDataContext(DbContextOptions<ProviderDataContext> options) :
            base(options) { }
    }
}