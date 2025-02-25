using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Golf;

namespace SportsData.Producer.Infrastructure.Data
{
    //public class DatabaseDesignTimeDbContextFactory
    //    : IDesignTimeDbContextFactory<BaseDataContext>
    //{
    //    public BaseDataContext CreateDbContext(string[] args)
    //    {
    //        var builder = new DbContextOptionsBuilder();
    //        builder.UseSqlServer();
    //        return new BaseDataContext(builder.Options);
    //        //var builder = new DbContextOptionsBuilder<AppDataContext>();
    //        //builder.UseSqlServer();
    //        //return new AppDataContext(builder.Options);
    //    }
    //}

    public class GolfDatabaseDesignTimeDbContextFactory
        : IDesignTimeDbContextFactory<GolfDataContext>
    {
        public GolfDataContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<GolfDataContext>();
            builder.UseSqlServer(new SqlConnection()
            {
                ConnectionString = "Server=192.168.0.3;Initial Catalog=sdProducer.Development.GolfPga;User=sa;Password=sesame1?;TrustServerCertificate=True;Encrypt=False"
            });
            return new GolfDataContext(builder.Options);
        }
    }

    public class FootballDatabaseDesignTimeDbContextFactory
        : IDesignTimeDbContextFactory<FootballDataContext>
    {
        public FootballDataContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<FootballDataContext>();
            builder.UseSqlServer(new SqlConnection()
            {
                ConnectionString = "Server=192.168.0.3;Initial Catalog=sdProducer.Development.Football;User=sa;Password=sesame1?;TrustServerCertificate=True;Encrypt=False"
            });
            return new FootballDataContext(builder.Options);
        }
    }
}
