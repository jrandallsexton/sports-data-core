using AutoFixture;

using AutoMapper;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq.AutoMock;

using SportsData.Producer.Infrastructure.Data;

namespace SportsData.Producer.Tests.Unit
{
    public abstract class UnitTestBase<T>
    {
        public AutoMocker Mocker { get; }

        public Fixture Fixture { get; }

        public ListLogger Logger { get; }

        public AppDataContext DataContext { get; }

        internal UnitTestBase()
        {
            Mocker = new AutoMocker();

            Fixture = new Fixture();

            DataContext = new AppDataContext(GetDataContextOptions());
            Mocker.Use(typeof(AppDataContext), DataContext);

            Logger = CreateLogger(LoggerTypes.List) as ListLogger;

            var mapperConfig = new MapperConfiguration(c => c.AddProfile(new DynamicMappingProfile()));
            var mapper = mapperConfig.CreateMapper();
            Mocker.Use(typeof(IMapper), mapper);
        }

        private static DbContextOptions<AppDataContext> GetDataContextOptions()
        {
            // https://stackoverflow.com/questions/52810039/moq-and-setting-up-db-context
            var dbName = Guid.NewGuid().ToString().Substring(0, 5);
            return new DbContextOptionsBuilder<AppDataContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }

        public static ILogger CreateLogger(LoggerTypes type = LoggerTypes.Null)
        {
            return type == LoggerTypes.List ?
                new ListLogger() :
                NullLoggerFactory.Instance.CreateLogger("Null Logger");
        }

        public async Task<string> LoadJsonTestData(string filename)
        {
            return await File.ReadAllTextAsync($"../../../Data/{filename}");
        }
    }
}
