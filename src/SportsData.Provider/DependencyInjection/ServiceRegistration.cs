using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using Hangfire;
using Polly;
using SportsData.Core.Common;
using SportsData.Core.Common.Parsing;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Http.Policies;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Config;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn;

namespace SportsData.Provider.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode, bool useMongo)
        {
            services.AddDataPersistenceExternal();

            // TODO: Get this wired to az app config
            var appConfig = new ProviderAppConfig()
            {
                IsDryRun = false,
                MaxResourceIndexItemsToProcess = null
            };

            services.AddSingleton<IProviderAppConfig>(appConfig);
            services.AddScoped<IProcessResourceIndexes, ResourceIndexJob>();
            services.AddScoped<IProcessResourceIndexItems, ResourceIndexItemProcessor>();
            services.AddScoped<IResourceIndexItemParser, ResourceIndexItemParser>();
            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();

            var imageClient = services.AddHttpClient("images", c =>
            {
                c.Timeout = TimeSpan.FromSeconds(15);
                c.DefaultRequestVersion = HttpVersion.Version20;
                c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                c.DefaultRequestHeaders.UserAgent.ParseAdd("SportDeets-Provider/1.0");
                c.DefaultRequestHeaders.Accept.ParseAdd("image/*");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 64,
                AutomaticDecompression = DecompressionMethods.All,
                SslOptions = new SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }
            });
            imageClient.AddPolicyHandler(Policy<HttpResponseMessage>
                .Handle<HttpRequestException>().Or<IOException>()
                .OrResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
                .WaitAndRetryAsync(3, i => TimeSpan.FromMilliseconds(200 * Math.Pow(2, i))));

            services.AddHttpClient<EspnHttpClient>()
                .AddPolicyHandler(RetryPolicy.GetRetryPolicy());

            services.AddScoped<IProvideEspnApiData, EspnApiClient>();
            services.AddScoped<IProcessPublishDocumentEvents, PublishDocumentEventsProcessor>();

            if (useMongo)
            {
                services.AddSingleton<IDocumentStore, MongoDocumentService>();
            }
            else
            {
                services.AddSingleton<IDocumentStore, CosmosDocumentService>();
            }

            // TODO: Move this to a config file
            services.AddSingleton(new EspnApiClientConfig()
            {
                ForceLiveFetch = false,
                PersistLocally = true,
                ReadFromCache = true,
                LocalCacheDirectory = "D:\\Dropbox\\Code\\sports-data\\data"
            });

            return services;
        }

        public static IServiceProvider ConfigureHangfireJobs(
            this IServiceProvider services,
            Sport mode)
        {
            var serviceScope = services.CreateScope();

            var recurringJobManager = serviceScope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
            recurringJobManager.AddOrUpdate<SourcingJobOrchestrator>(
                nameof(SourcingJobOrchestrator),
                job => job.ExecuteAsync(),
                Cron.Minutely);

            return services;
        }

    }
}
