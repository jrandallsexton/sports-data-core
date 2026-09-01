using AutoFixture;
using AutoFixture.Kernel;

using AutoMapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Producer.Application.Documents.Processors.Commands;
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
        Fixture.Customizations.Add(new TypeRelay(typeof(CompetitionSituationBase), typeof(FootballCompetitionSituation)));
        Fixture.Customizations.Add(new TypeRelay(typeof(CompetitionStatusBase), typeof(FootballCompetitionStatus)));
        Fixture.Customizations.Add(new TypeRelay(typeof(CompetitionCompetitorBase), typeof(FootballCompetitionCompetitor)));

        // Pin ProcessDocumentCommand's boolean flags (NotifyOnCompletion today) to
        // false for fixture-built commands. AutoFixture fills booleans by
        // ALTERNATION, so which flag lands true depends on how many bools the
        // fixture handed out before it — adding a bool parameter anywhere in the
        // command flips the parity for every test downstream. Discovered 2026-09-01
        // when a prototype bool on the command turned two green AthleteSeason guard
        // tests red: NotifyOnCompletion silently became true and the base class
        // published DocumentProcessingCompleted, failing their "publishes nothing"
        // assertions. EventCompetitionOddsDocumentProcessorTests had already been
        // bitten and pins the flag per call site. A specimen builder (not
        // Customize<T>) because Fixture.Build<T> bypasses Customize<T> — this
        // intercepts the ctor-parameter and property requests themselves, and an
        // explicit .With(...) in a test still wins.
        Fixture.Customizations.Add(new ProcessDocumentCommandFlagsOffByDefault());

        // Override mapper with Producer-specific mapping profile
        var mapperConfig = new MapperConfiguration(c =>
        {
            c.AddProfile(new DynamicMappingProfile());
            c.AddProfile(new MappingProfile());
        });
        var mapper = mapperConfig.CreateMapper();
        Mocker.Use(typeof(IMapper), mapper);
    }

    private sealed class ProcessDocumentCommandFlagsOffByDefault : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (request is System.Reflection.ParameterInfo pi
                && pi.Member.DeclaringType == typeof(ProcessDocumentCommand)
                && pi.ParameterType == typeof(bool))
            {
                return false;
            }

            if (request is System.Reflection.PropertyInfo prop
                && prop.DeclaringType == typeof(ProcessDocumentCommand)
                && prop.PropertyType == typeof(bool))
            {
                return false;
            }

            return new NoSpecimen();
        }
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
