using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SportsData.Core.Common;
using SportsData.Core.Config;
using SportsData.Core.DependencyInjection;

namespace SportsData.Tests.Shared;

public abstract class IntegrationTestBase<T> where T : class
{
    protected readonly IConfiguration Configuration;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly CommonConfig CommonConfig;

    protected IntegrationTestBase(string label = "Dev", Sport mode = Sport.All)
    {
        var builder = new ConfigurationManager();

        // PRELOAD secrets manually (required so APPCONFIG_CONNSTR is available!)
        builder.AddUserSecrets<CommonConfig>();
        builder.AddUserSecrets(typeof(T).Assembly);
        builder.AddUserSecrets(typeof(IntegrationTestBase<>).Assembly);

        // Now AddCommonConfiguration can see APPCONFIG_CONNSTR
        var config = builder.AddCommonConfiguration(
            environmentName: label,
            applicationName: typeof(T).Assembly.GetName().Name ?? "Unknown",
            mode: mode
        );

        Configuration = config;

        var services = new ServiceCollection();
        services.Configure<CommonConfig>(config.GetSection("CommonConfig"));
        ServiceProvider = services.BuildServiceProvider();

        CommonConfig = ServiceProvider.GetRequiredService<IOptions<CommonConfig>>().Value;
    }

}