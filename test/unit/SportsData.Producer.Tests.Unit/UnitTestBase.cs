using AutoFixture;

using AutoMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq.AutoMock;

using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Tests.Unit
{
    // TODO: Move this to Core and share it
    public abstract class UnitTestBase<T>
    {
        public AutoMocker Mocker { get; }

        public Fixture Fixture { get; }

        public ListLogger Logger { get; }

        public FootballDataContext FootballDataContext { get; }

        public TeamSportDataContext TeamSportDataContext => FootballDataContext;

        internal UnitTestBase()
        {
            Mocker = new AutoMocker();

            Fixture = new Fixture();

            FootballDataContext = new FootballDataContext(GetFootballDataContextOptions());
            Mocker.Use(typeof(BaseDataContext), FootballDataContext);
            Mocker.Use(typeof(TeamSportDataContext), FootballDataContext);
            Mocker.Use(FootballDataContext);

            Logger = CreateLogger(LoggerTypes.List) as ListLogger;

            var mapperConfig = new MapperConfiguration(c => c.AddProfile(new DynamicMappingProfile()));
            var mapper = mapperConfig.CreateMapper();
            Mocker.Use(typeof(IMapper), mapper);
        }

        private static DbContextOptions<FootballDataContext> GetFootballDataContextOptions()
        {
            // https://stackoverflow.com/questions/52810039/moq-and-setting-up-db-context
            var dbName = Guid.NewGuid().ToString().Substring(0, 5);
            return new DbContextOptionsBuilder<FootballDataContext>()
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
