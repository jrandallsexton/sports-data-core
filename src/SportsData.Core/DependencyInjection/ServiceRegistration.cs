using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.Tags.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Common.Routing;
using SportsData.Core.Config;
using SportsData.Core.Http.Policies;
using SportsData.Core.Infrastructure.Blobs;
using SportsData.Core.Infrastructure.Clients;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Middleware.Health;
using SportsData.Provider.Infrastructure.Providers.Espn;

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace SportsData.Core.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration config)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = config[CommonConfigKeys.CacheServiceUri];

                // TODO: Determine how to pass in an instance name from each consumer
                // i.e. sdApi, sdContest, sdVenue, etc.
                // (or have it generated here based on EnvironmentName and ApplicationName
                options.InstanceName = "sdapi_"; // (only one app using; good practice)
            });

            return services;
        }

        public static IServiceCollection AddDataPersistence<T>(
            this IServiceCollection services,
            IConfiguration configuration,
            string applicationName,
            Sport mode) where T : DbContext
        {
            // TODO: Clean up this hacky mess
            var cc = configuration.GetSection("CommonConfig")["SqlBaseConnectionString"];
            var connString = $"{cc};Database=sd{applicationName.Replace("SportsData.", string.Empty)}.{mode}";

            Console.WriteLine($"using: {connString}");

            services.AddDbContext<T>(options =>
            {
                options.EnableSensitiveDataLogging();
                options.UseNpgsql(connString);
            });

            return services;
        }

        /// <summary>
        /// Adds Azure Blob Storage
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddDataPersistenceExternal(this IServiceCollection services)
        {
            services.AddScoped<IProvideBlobStorage, BlobStorageProvider>();
            return services;
        }

        /// <summary>
        /// MUST be called last after ALL services have been added/configured
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="services"></param>
        /// <param name="seedFunction"></param>
        /// <returns></returns>
        public static async Task<IServiceCollection> ApplyMigrations<T>(
            this IServiceCollection services,
            Func<T, Task>? seedFunction) where T : DbContext
        {
            await using var serviceProvider = services.BuildServiceProvider();
            var context = serviceProvider.GetRequiredService<T>();
            var pending = await context.Database.GetPendingMigrationsAsync();
            if (pending.Any())
                await context.Database.MigrateAsync();

            if (seedFunction is not null)
                await seedFunction(context);
            return services;
        }

        public static async Task ApplyMigrations<T>(
            this IServiceProvider services,
            Func<T, Task>? seedFunction = null) where T : DbContext
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<T>();
            var pending = await context.Database.GetPendingMigrationsAsync();
            if (pending.Any())
                await context.Database.MigrateAsync();

            if (seedFunction is not null)
                await seedFunction(context);
        }


        public static IServiceCollection AddCoreServices(
            this IServiceCollection services,
            IConfiguration configuration,
            Sport mode = Sport.All)
        {
            services.AddScoped<IDecodeDocumentProvidersAndTypes, DocumentProviderAndTypeDecoder>();
            services.Configure<CommonConfig>(configuration.GetSection("CommonConfig"));
            services.AddScoped<IDateTimeProvider, DateTimeProvider>();
            services.AddSingleton<IAppMode>(new AppMode(mode));
            services.AddScoped<IGenerateRoutingKeys, RoutingKeyGenerator>();
            services.AddScoped<IJsonHashCalculator, JsonHashCalculator>();
            services.AddSingleton<IGenerateExternalRefIdentities, ExternalRefIdentityGenerator>();
            return services;
        }

        public static IServiceCollection AddHangfire(
            this IServiceCollection services,
            IConfiguration configuration,
            string applicationName,
            Sport mode,
            int? maxWorkers)
        {
            // TODO: Clean up this hacky mess
            var cc = configuration.GetSection("CommonConfig")["SqlBaseConnectionString"];
            var connString = $"{cc};Database=sd{applicationName.Replace("SportsData.", string.Empty)}.{mode}.Hangfire";

            Console.WriteLine($"Hangfire ConnStr: {connString}");

            services.AddHangfire(x =>
            {
                x.UsePostgreSqlStorage(options =>
                {
                    options.UseNpgsqlConnection(connString);
                });
                x.UseTagsWithPostgreSql();
            });

            services.AddHangfireServer(serverOptions =>
            {
                // https://codeopinion.com/scaling-hangfire-process-more-jobs-concurrently/
                serverOptions.WorkerCount = maxWorkers ?? Environment.ProcessorCount * 2;
                
            });
            return services;
        }

        public static IServiceCollection AddHealthChecks<TDbContext, TPublisher>(
            this IServiceCollection services,
            string apiName,
            Sport mode)
            where TDbContext : DbContext where TPublisher : class
        {
            services.AddHealthChecks()
                .AddCheck<HealthCheck>($"{apiName}-{mode}")
                .AddCheck<LoggingHealthCheck>("logging")
                .AddCheck<DatabaseHealthCheck<TDbContext>>($"{apiName}-db");

            services.AddHostedService<HeartbeatPublisher<TPublisher>>();

            return services;
        }

        public static IServiceCollection AddHealthChecksMaster<TPublisher>(
            this IServiceCollection services,
            string apiName)
            where TPublisher : class
        {
            services.AddHealthChecks()
                .AddCheck<HealthCheck>(apiName)
                //.AddCheck<CachingHealthCheck>("caching")
                .AddCheck<LoggingHealthCheck>("logging")
                //.AddCheck<ClientHealthCheck<IProvideContests>>(HttpClients.ContestClient)
                //.AddCheck<ClientHealthCheck<IProvideFranchises>>(HttpClients.FranchiseClient)
                //.AddCheck<ClientHealthCheck<IProvideNotifications>>(HttpClients.NotificationClient)
                //.AddCheck<ClientHealthCheck<IProvidePlayers>>(HttpClients.PlayerClient)
                //.AddCheck<ClientHealthCheck<IProvideProducers>>(HttpClients.ProducerClient)
                .AddCheck<ClientHealthCheck<IProvideProviders>>(HttpClients.ProviderClient);
            //.AddCheck<ClientHealthCheck<IProvideSeasons>>(HttpClients.SeasonClient)
            //.AddCheck<ClientHealthCheck<IProvideVenues>>(HttpClients.VenueClient);

            services.AddHostedService<HeartbeatPublisher<TPublisher>>();

            return services;
        }

        public static IServiceCollection AddInstrumentation(this IServiceCollection services, string applicationName)
        {
            Action<ResourceBuilder> appResourceBuilder =
                resource => resource
                    .AddTelemetrySdk()
                    .AddService(applicationName);

            services.AddOpenTelemetry()
                .ConfigureResource(appResourceBuilder)
                .WithTracing(builder => builder
                    .SetSampler<AlwaysOnSampler>()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("APITracing")
                    //.AddConsoleExporter()
                    .AddOtlpExporter(options => options.Endpoint = new Uri("http://localhost:4317/v1/logs"))
                )
                .WithMetrics(builder => builder
                    .AddRuntimeInstrumentation()
                    .AddMeter(
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Server.Kestrel",
                        "System.Net.Http")
                    .AddPrometheusExporter()
                    .AddAspNetCoreInstrumentation()
                    .AddOtlpExporter(options => options.Endpoint = new Uri("http://localhost:4317/v1/logs")));

            services.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddOtlpExporter());
            services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());

            return services;
        }

        public static IServiceCollection AddMediatR(this IServiceCollection services, Assembly assembly)
        {
            services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));
            return services;
        }

        public static IServiceCollection AddClients(this IServiceCollection services, IConfiguration configuration, Sport mode = Sport.All)
        {
            var registry = services.AddPolicyRegistry();
            registry.Add("HttpRetry", RetryPolicy.GetRetryPolicy());

            // Enables IHttpClientFactory for named clients
            services.AddHttpClient();

            /* ESPN */

            services.Configure<EspnApiClientConfig>(
                configuration.GetSection("SportsData.Provider:EspnApiClientConfig")
            );

            services.AddHttpClient<EspnHttpClient>()
                .AddPolicyHandler(RetryPolicy.GetRetryPolicy());

            services.AddScoped<IProvideEspnApiData, EspnApiClient>();

            /* End ESPN */

            // Register single-mode services
            services
                .AddHttpClient<IProvideProviders, ProviderClient>(HttpClients.ProviderClient, c =>
                {
                    c.BaseAddress = new Uri(configuration[CommonConfigKeys.GetProviderProviderUri()]!);
                    c.Timeout = TimeSpan.FromSeconds(15);
                    c.DefaultRequestVersion = HttpVersion.Version20;
                    c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                })
                .AddPolicyHandlerFromRegistry("HttpRetry");

            //services
            //    .AddHttpClient<IProvideProducers, ProducerClient>(HttpClients.ProducerClient, c =>
            //    {
            //        c.BaseAddress = new Uri(configuration[CommonConfigKeys.GetProducerProviderUri()]!);
            //    })
            //    .AddPolicyHandlerFromRegistry("HttpRetry");

            // VenueClient is handled via factory instead
            services.AddSingleton<IVenueClientFactory, VenueClientFactory>();

            // Register venue clients by mode (for use by factory)
            var supportedModes = configuration.GetSection("CommonConfig:Api:SupportedModes").Get<Sport[]>();
            if (supportedModes != null)
            {
                foreach (var sport in supportedModes)
                {
                    var apiUrl = configuration[CommonConfigKeys.GetVenueProviderUri()];
                    if (!string.IsNullOrEmpty(apiUrl))
                    {
                        var clientName = $"{HttpClients.VenueClient}";
                        services.AddHttpClient(clientName, client => client.BaseAddress = new Uri(apiUrl));
                    }
                }
            }

            return services;
        }

        private static IServiceCollection AddClient<TService, TImplementation>(
            this IServiceCollection services,
            IConfiguration configuration,
            string clientName,
            string clientUrlKey)
            where TService : class
            where TImplementation : class, TService
        {
            services.AddScoped<TService, TImplementation>();

            // Get the supported sports from configuration
            var supportedSports = configuration.GetSection("CommonConfig:Api:SupportedModes").Get<Sport[]>();
            if (supportedSports == null || !supportedSports.Any())
            {
                throw new InvalidOperationException("No supported sports configured");
            }

            // Create a named HTTP client for each supported sport
            foreach (var sport in supportedSports)
            {
                var apiUrl = configuration[clientUrlKey];
                if (string.IsNullOrEmpty(apiUrl))
                {
                    continue; // Skip if no URL configured for this sport
                }

                services.AddHttpClient($"{clientName}", client =>
                {
                    client.BaseAddress = new Uri(apiUrl);
                });
            }

            return services;
        }

    }
}
