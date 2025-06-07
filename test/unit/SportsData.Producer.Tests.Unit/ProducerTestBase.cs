using Microsoft.EntityFrameworkCore;

using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Tests.Shared;

namespace SportsData.Producer.Tests.Unit;

public abstract class ProducerTestBase<T> : UnitTestBase<T>
    where T : class
{
    public FootballDataContext FootballDataContext { get; }

    public TeamSportDataContext TeamSportDataContext => FootballDataContext;

    internal ProducerTestBase()
    {
        FootballDataContext = new FootballDataContext(GetFootballDataContextOptions());
        Mocker.Use(typeof(BaseDataContext), FootballDataContext);
        Mocker.Use(typeof(TeamSportDataContext), FootballDataContext);
        Mocker.Use(FootballDataContext);
    }

    private static DbContextOptions<FootballDataContext> GetFootballDataContextOptions()
    {
        // https://stackoverflow.com/questions/52810039/moq-and-setting-up-db-context
        var dbName = Guid.NewGuid().ToString().Substring(0, 5);
        return new DbContextOptionsBuilder<FootballDataContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
    }
}