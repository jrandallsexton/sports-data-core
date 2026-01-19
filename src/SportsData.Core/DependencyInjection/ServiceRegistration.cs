using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.Tags.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Venue;
using SportsData.Core.Infrastructure.Clients.YouTube;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.Refs;
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
            var connString = $"{cc};Database=sd{applicationName.Replace("SportsData.", string.Empty)}.{mode};Include Error Detail=true;";

#if DEBUG
            Console.WriteLine($"using: {connString}");
#endif

            services.AddDbContext<T>(options =>
            {
                options.EnableSensitiveDataLogging();
                options.UseNpgsql(connString, builder =>
                {
                    builder.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), ["40001"]);
                });
                options.ConfigureWarnings(w =>
                    w.Throw(RelationalEventId.MultipleCollectionIncludeWarning));

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
            services.AddSingleton<IGenerateResourceRefs, ResourceRefGenerator>();
            return services;
        }

        public static IServiceCollection AddHangfire(
            this IServiceCollection services,
            IConfiguration configuration,
            string applicationName,
            Sport mode)
        {
            // TODO: Clean up this hacky mess
            var cc = configuration.GetSection("CommonConfig")["SqlBaseConnectionString"];
            var connString = $"{cc};Database=sd{applicationName.Replace("SportsData.", string.Empty)}.{mode}.Hangfire";

            var minWorkersConfigValue = configuration[$"{applicationName}:BackgroundProcessor:MinWorkers"];
            var minWorkers = int.TryParse(minWorkersConfigValue, out var parsedMinWorkers)
                ? parsedMinWorkers
                : 20;

#if DEBUG
            Console.WriteLine($"Hangfire ConnStr: {connString}");
#endif

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
                serverOptions.WorkerCount = minWorkers;
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
                //.AddCheck<LoggingHealthCheck>("logging")
                //.AddCheck<ClientHealthCheck<IProvideContests>>(HttpClients.ContestClient)
                //.AddCheck<ClientHealthCheck<IProvideFranchises>>(HttpClients.FranchiseClient)
                //.AddCheck<ClientHealthCheck<IProvideNotifications>>(HttpClients.NotificationClient)
                //.AddCheck<ClientHealthCheck<IProvidePlayers>>(HttpClients.PlayerClient)
                .AddCheck<ClientHealthCheck<IProvideProviders>>(HttpClients.ProviderClient);
                //.AddCheck<ClientHealthCheck<IProvideSeasons>>(HttpClients.SeasonClient)
                //.AddCheck<ClientHealthCheck<IProvideVenues>>(HttpClients.VenueClient);

            services.AddHostedService<HeartbeatPublisher<TPublisher>>();

            return services;
        }

        public static IServiceCollection AddInstrumentation(
            this IServiceCollection services,
            string applicationName,
            IConfiguration configuration)
        {
            var otelConfig = configuration.GetSection("CommonConfig:" + OpenTelemetryConfig.SectionName).Get<OpenTelemetryConfig>();

            // If OTel is disabled or config is missing, skip instrumentation
            if (otelConfig == null || !otelConfig.Enabled)
            {
                return services;
            }

            // Use configured service name or fallback to application name
            var serviceName = string.IsNullOrEmpty(otelConfig.ServiceName) ? applicationName : otelConfig.ServiceName;

            Action<ResourceBuilder> appResourceBuilder =
                resource => resource
                    .AddTelemetrySdk()
                    .AddService(
                        serviceName: serviceName,
                        serviceVersion: otelConfig.ServiceVersion);

            var otelBuilder = services.AddOpenTelemetry()
                .ConfigureResource(appResourceBuilder);

            // === TRACING ===
            if (otelConfig.Tracing.Enabled)
            {
                otelBuilder.WithTracing(builder =>
                {
                    // Use configured sampling ratio
                    var samplingRatio = Math.Clamp(otelConfig.Tracing.SamplingRatio, 0.0, 1.0);
                    builder.SetSampler(new TraceIdRatioBasedSampler(samplingRatio));

                    // Built-in instrumentation
                    builder.AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = httpContext =>
                        {
                            // Don't trace health checks
                            return !httpContext.Request.Path.StartsWithSegments("/health");
                        };
                    });

                    builder.AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    });

                    // Custom activity sources
                    builder.AddSource("SportsData.*");

                    // Export to OTLP (Tempo)
                    builder.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otelConfig.Tracing.OtlpEndpoint);
                        options.TimeoutMilliseconds = otelConfig.Tracing.TimeoutMs;
                    });
                });
            }

            // === METRICS ===
            if (otelConfig.Metrics.Enabled)
            {
                otelBuilder.WithMetrics(builder =>
                {
                    // Built-in meters
                    builder.AddRuntimeInstrumentation();
                    builder.AddAspNetCoreInstrumentation();
                    builder.AddHttpClientInstrumentation();

                    // Framework meters
                    builder.AddMeter(
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Server.Kestrel",
                        "System.Net.Http");

                    // Prometheus scraping endpoint
                    builder.AddPrometheusExporter(options =>
                    {
                        // Metrics available at /metrics
                    });

                    // Also export to OTLP if endpoint is configured
                    if (!string.IsNullOrEmpty(otelConfig.Metrics.OtlpEndpoint))
                    {
                        builder.AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(otelConfig.Metrics.OtlpEndpoint);
                            options.TimeoutMilliseconds = otelConfig.Metrics.TimeoutMs;
                        });
                    }
                });
            }

            // === LOGGING ===
            // Note: Loki doesn't support OTLP natively - this requires an OTLP Collector
            if (otelConfig.Logging.Enabled)
            {
                services.Configure<OpenTelemetryLoggerOptions>(logging =>
                {
                    logging.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otelConfig.Logging.OtlpEndpoint);
                        options.TimeoutMilliseconds = otelConfig.Logging.TimeoutMs;
                    });
                });
            }

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

            services.AddHttpClient();
            services.AddSeqClient();
            services.AddEspnClient(configuration);
            services.AddYouTubeClient(configuration);
            services.AddProviderClient(configuration);
            services.AddClientFactories();
            services.AddModeAgnosticClients(configuration);

            return services;
        }

        private static IServiceCollection AddSeqClient(this IServiceCollection services)
        {
            services.AddHttpClient("Seq", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
            });

            return services;
        }

        private static IServiceCollection AddEspnClient(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<EspnApiClientConfig>(
                configuration.GetSection("SportsData.Provider:EspnApiClientConfig"));

            services.AddHttpClient<EspnHttpClient>(client =>
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; SportDeets/1.0; +https://sportdeets.com)");
                })
                .AddPolicyHandler(RetryPolicy.GetRetryPolicy());

            services.AddScoped<IProvideEspnApiData, EspnApiClient>();

            return services;
        }

        private static IServiceCollection AddYouTubeClient(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<YouTubeClientConfig>(
                configuration.GetSection("CommonConfig:YouTubeClientConfig"));

            services
                .AddHttpClient<IProvideYouTube, YouTubeHttpClient>(HttpClients.YouTubeClient, (sp, c) =>
                {
                    var config = sp.GetRequiredService<IOptions<YouTubeClientConfig>>().Value;
                    c.BaseAddress = new Uri("https://youtube.googleapis.com/youtube/v3/");
                    c.Timeout = TimeSpan.FromSeconds(15);
                    c.DefaultRequestVersion = HttpVersion.Version20;
                    c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                })
                .AddPolicyHandlerFromRegistry("HttpRetry");

            return services;
        }

        private static IServiceCollection AddProviderClient(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddHttpClient<IProvideProviders, ProviderClient>(HttpClients.ProviderClient, c =>
                {
                    c.BaseAddress = new Uri(configuration[CommonConfigKeys.GetProviderProviderUri()]!);
                    c.Timeout = TimeSpan.FromSeconds(15);
                    c.DefaultRequestVersion = HttpVersion.Version20;
                    c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                })
                .AddPolicyHandlerFromRegistry("HttpRetry");

            return services;
        }

        private static IServiceCollection AddClientFactories(this IServiceCollection services)
        {
            services.AddSingleton<IVenueClientFactory, VenueClientFactory>();
            services.AddSingleton<IFranchiseClientFactory, FranchiseClientFactory>();
            services.AddSingleton<IContestClientFactory, ContestClientFactory>();

            return services;
        }

        private static IServiceCollection AddModeAgnosticClients(this IServiceCollection services, IConfiguration configuration)
        {
            var contestApiUrl = configuration[CommonConfigKeys.GetContestProviderUri()];
            if (!string.IsNullOrEmpty(contestApiUrl))
            {
                services.AddHttpClient(HttpClients.ContestClient, client => client.BaseAddress = new Uri(contestApiUrl));
            }

            var venueApiUrl = configuration[CommonConfigKeys.GetVenueProviderUri()];
            if (!string.IsNullOrEmpty(venueApiUrl))
            {
                services.AddHttpClient(HttpClients.VenueClient, client => client.BaseAddress = new Uri(venueApiUrl));
            }

            var franchiseApiUrl = configuration[CommonConfigKeys.GetFranchiseProviderUri()];
            if (!string.IsNullOrEmpty(franchiseApiUrl))
            {
                services.AddHttpClient(HttpClients.FranchiseClient, client => client.BaseAddress = new Uri(franchiseApiUrl));
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
