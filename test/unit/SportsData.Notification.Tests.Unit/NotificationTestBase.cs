using Microsoft.EntityFrameworkCore;

using SportsData.Notification.Infrastructure.Data;
using SportsData.Tests.Shared;

namespace SportsData.Notification.Tests.Unit
{
    public abstract class NotificationTestBase<T> : UnitTestBase<T>
        where T : class
    {
        public AppDataContext DataContext { get; }

        protected NotificationTestBase()
        {
            DataContext = new AppDataContext(GetAppDataContextOptions());
            Mocker.Use(typeof(AppDataContext), DataContext);
        }

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
