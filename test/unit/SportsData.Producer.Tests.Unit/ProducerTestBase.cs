using AutoMapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Producer;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Mapping;
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
        
        // Override mapper with Producer-specific mapping profile
        var mapperConfig = new MapperConfiguration(c =>
        {
            c.AddProfile(new DynamicMappingProfile());
            c.AddProfile(new MappingProfile());
        });
        var mapper = mapperConfig.CreateMapper();
        Mocker.Use(typeof(IMapper), mapper);
    }

    private static DbContextOptions<FootballDataContext> GetFootballDataContextOptions()
    {
        // https://stackoverflow.com/questions/52810039/moq-and-setting-up-db-context
        var dbName = Guid.NewGuid().ToString()[..5];
        return new DbContextOptionsBuilder<FootballDataContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
    }
}