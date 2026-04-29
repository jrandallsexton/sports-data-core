using System.Runtime.CompilerServices;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SportsData.Core.Config;
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

            // CommonConfig has many `required` members the handlers under test never read.
            // Use GetUninitializedObject so we don't have to maintain stub values for every
            // field as the config evolves. CurrentSeason defaults to 0, which the cache
            // policy treats as "feature disabled, always bypass" — the safe legacy path
            // existing tests already assume.
            var commonConfig = (CommonConfig)RuntimeHelpers.GetUninitializedObject(typeof(CommonConfig));
            Mocker.Use<IOptions<CommonConfig>>(Options.Create(commonConfig));
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
