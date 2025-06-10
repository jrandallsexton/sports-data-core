using Microsoft.EntityFrameworkCore;

using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Baseball
{
    public class BaseballDataContext(DbContextOptions<BaseballDataContext> options) :
        BaseDataContext(options)
    {
        public new DbSet<BaseballAthlete> Athletes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new Athlete.EntityConfiguration());
        }
    }
}
