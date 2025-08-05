using Microsoft.EntityFrameworkCore;

using SportsData.Provider.Infrastructure.Data;
using SportsData.Tests.Shared;

namespace SportsData.Provider.Tests.Unit
{
    public abstract class ProviderTestBase<T> : UnitTestBase<T>
        where T : class
    {
        public AppDataContext DataContext { get; }

        internal ProviderTestBase()
        {
            DataContext = new AppDataContext(GetAppDataContextOptions());
            Mocker.Use(typeof(AppDataContext), DataContext);
            Mocker.Use(DataContext);
        }

        private static DbContextOptions<AppDataContext> GetAppDataContextOptions()
        {
            var dbName = Guid.NewGuid().ToString()[..5];
            return new DbContextOptionsBuilder<AppDataContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }
    }
}
