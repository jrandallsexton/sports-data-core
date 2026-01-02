using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Tests.Shared;

namespace SportsData.Api.Tests.Unit
{
    public abstract class ApiTestBase<T> : UnitTestBase<T>
        where T : class
    {
        public AppDataContext DataContext { get; }

        protected ApiTestBase()
        {
            DataContext = new AppDataContext(GetAppDataContextOptions());
            Mocker.Use(typeof(AppDataContext), DataContext);
            Mocker.Use(DataContext);
        }

        /// <summary>
        /// Creates DbContext options configured to use a uniquely named in-memory AppDataContext database.
        /// </summary>
        /// <returns>DbContextOptions for AppDataContext that target a uniquely named in-memory database and ignore EF Core's in-memory transaction ignored warning.</returns>
        private static DbContextOptions<AppDataContext> GetAppDataContextOptions()
        {
            var dbName = Guid.NewGuid().ToString()[..5];
            return new DbContextOptionsBuilder<AppDataContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }
    }
}