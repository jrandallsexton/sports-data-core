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

    public ListLogger Logger { get; }

    protected UnitTestBase()
    {
        Mocker = new AutoMocker();

        Fixture = new Fixture();

        Logger = CreateLogger(LoggerTypes.List) as ListLogger;

        var mapperConfig = new MapperConfiguration(c => c.AddProfile(new DynamicMappingProfile()));
        var mapper = mapperConfig.CreateMapper();
        Mocker.Use(typeof(IMapper), mapper);
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