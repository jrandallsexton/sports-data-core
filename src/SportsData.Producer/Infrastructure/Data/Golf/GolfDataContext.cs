
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Extensions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Golf.Entities;

namespace SportsData.Producer.Infrastructure.Data.Golf
{

    public class GolfDataContext(DbContextOptions<GolfDataContext> options) :
        BaseDataContext(options)
    {
        public new DbSet<GolfAthlete> Athletes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.WithUriConverter();
            modelBuilder.ApplyConfiguration(new Athlete.EntityConfiguration());
        }
    }
}
