using System.Runtime.CompilerServices;

using AutoFixture.Kernel;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SportsData.Core.Config;
using SportsData.Core.Eventing.Events.Documents;
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

            // Pin DocumentRequested's boolean members (Priority today) to false
            // for fixture-built events. AutoFixture fills bools by ALTERNATION,
            // so adding a bool to the record flips which events land true
            // across the whole suite — introducing Priority (live-queue
            // routing) turned 23 green handler tests red because randomly-
            // prioritized events routed to the queue-targeted Enqueue overload
            // the mocks didn't capture. Same lesson as ProducerTestBase's
            // ProcessDocumentCommand pin; a specimen builder (not Customize<T>)
            // because Fixture.Build<T> bypasses Customize<T>, and an explicit
            // .With(...) in a test still wins.
            Fixture.Customizations.Add(new DocumentRequestedFlagsOffByDefault());
        }

        private sealed class DocumentRequestedFlagsOffByDefault : ISpecimenBuilder
        {
            public object Create(object request, ISpecimenContext context)
            {
                if (request is System.Reflection.ParameterInfo pi
                    && pi.Member.DeclaringType == typeof(DocumentRequested)
                    && pi.ParameterType == typeof(bool))
                {
                    return false;
                }

                if (request is System.Reflection.PropertyInfo prop
                    && prop.DeclaringType == typeof(DocumentRequested)
                    && prop.PropertyType == typeof(bool))
                {
                    return false;
                }

                return new NoSpecimen();
            }
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
