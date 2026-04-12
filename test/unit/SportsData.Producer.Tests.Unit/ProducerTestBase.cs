using AutoFixture;
using AutoFixture.Kernel;

using AutoMapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;
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

        // Map abstract entity types to football subtypes for AutoFixture.
        // This allows tests using Fixture.Build<ContestBase>() etc. to work
        // without every test explicitly using the sport-specific subclass.
        Fixture.Customizations.Add(new TypeRelay(typeof(ContestBase), typeof(FootballContest)));
        Fixture.Customizations.Add(new TypeRelay(typeof(CompetitionBase), typeof(FootballCompetition)));
        Fixture.Customizations.Add(new TypeRelay(typeof(CompetitionPlayBase), typeof(FootballCompetitionPlay)));

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
