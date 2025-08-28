using Azure.Identity;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SportsData.Core.Config;
using SportsData.Core.Infrastructure.Blobs;

using Xunit;

namespace SportsData.Core.Tests.Integration.Infrastructure.Blobs;

public class BlobStorageProviderTests
{
    private readonly IProvideBlobStorage _blobStorageProvider;

    public BlobStorageProviderTests()
    {
        // Load user secrets including APPCONFIG_CONNSTR
        var tempConfig = new ConfigurationBuilder()
            .AddUserSecrets<BlobStorageProviderTests>()
            .Build();

        var appConfigConnectionString = tempConfig["APPCONFIG_CONNSTR"];
        if (string.IsNullOrWhiteSpace(appConfigConnectionString))
            throw new InvalidOperationException("Missing APPCONFIG_CONNSTR in user secrets.");

        // Now load Azure App Configuration using the secret
        var config = new ConfigurationBuilder()
            .AddAzureAppConfiguration(options =>
            {
                options.Connect(appConfigConnectionString)
                    .Select("CommonConfig", "Dev")
                    .Select("CommonConfig:*", "Dev")
                    .ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });
            })
            .Build();

        // Set up DI and bind CommonConfig
        var services = new ServiceCollection();

        services.Configure<CommonConfig>(config.GetSection("CommonConfig"));
        services.AddSingleton<IProvideBlobStorage, BlobStorageProvider>();

        var provider = services.BuildServiceProvider();
        _blobStorageProvider = provider.GetRequiredService<IProvideBlobStorage>();
    }

    [Fact]
    public async Task CanDownloadPromptText()
    {
        var result = await _blobStorageProvider.GetFileContentsAsync("prompts", "prediction-insights-v1.txt");

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.Contains("plausible", result); // Adjust based on actual prompt content
    }
}