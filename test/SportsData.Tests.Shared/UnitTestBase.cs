using AutoFixture;

using AutoMapper;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq.AutoMock;

using SportsData.Core.Common.Mapping;

namespace SportsData.Tests.Shared;

public abstract class UnitTestBase<T>
{
    public AutoMocker Mocker { get; }

    public Fixture Fixture { get; }

    public ListLogger? Logger { get; }

    protected UnitTestBase()
    {
        Mocker = new AutoMocker();

        Fixture = new Fixture();

        Fixture.Behaviors
            .OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => Fixture.Behaviors.Remove(b));

        Fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        Logger = CreateLogger(LoggerTypes.List) as ListLogger;

        Mocker.Use(typeof(IMapper), SharedTestMappers.Default.Value);
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

/// <summary>
/// Non-generic holder for the shared test mapper. AutoMapper configuration
/// compilation is expensive and xunit constructs the test class PER TEST —
/// building it in the UnitTestBase ctor charged every test in every suite a
/// fresh compile. A static on UnitTestBase&lt;T&gt; itself (or any type
/// nested in it) would be re-created once per closed T (~one per test
/// class), so the cache lives here: exactly one compile per profile set.
/// IMapper is stateless and thread-safe, so one instance serves parallel
/// runs. Derived bases that need extra profiles (e.g. ProducerTestBase)
/// overwrite the registration with their own non-generic-held instance.
/// </summary>
internal static class SharedTestMappers
{
    internal static readonly Lazy<IMapper> Default = new(() =>
        new MapperConfiguration(c => c.AddProfile(new DynamicMappingProfile())).CreateMapper());
}