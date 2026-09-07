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

        Mocker.Use(typeof(IMapper), SharedMapper.Value);
    }

    // AutoMapper configuration compilation is expensive and xunit constructs
    // the test class PER TEST — building it in the ctor charged every test in
    // every suite a fresh compile. IMapper is stateless and thread-safe, so
    // one shared instance serves parallel test runs. Derived bases that need
    // extra profiles (e.g. ProducerTestBase) overwrite the registration with
    // their own cached instance.
    private static readonly Lazy<IMapper> SharedMapper = new(() =>
        new MapperConfiguration(c => c.AddProfile(new DynamicMappingProfile())).CreateMapper());

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