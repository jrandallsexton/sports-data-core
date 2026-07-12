using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.Tags.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Infrastructure.Clients.Venue;
using SportsData.Core.Infrastructure.Clients.YouTube;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Core.Middleware.Health;
using SportsData.Provider.Infrastructure.Providers.Espn;

using StackExchange.Redis;

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
        // Hard ceiling to prevent a misconfigured value from saturating PostgreSQL's max_connections (500).
        // Each worker pod opens 2 pools (data context + Hangfire), so per-pool max must stay well below 500/pods.
        private const int MaxAllowedPoolSize = 50;

        /// <summary>
        /// Resolves connection pool size from Azure App Config, falling back to a hardcoded default.
        /// Config key: {applicationName}:ConnectionPool:{roleName}
        /// Values are clamped to <see cref="MaxAllowedPoolSize"/> to prevent accidental saturation.
        /// </summary>
        public static int ResolvePoolSize(
            IConfiguration configuration,
            string applicationName,
            string roleName,
            int defaultPoolSize)
        {
            var configValue = configuration[$"{applicationName}:ConnectionPool:{roleName}"];
            // Clamp both the App Config value AND the hardcoded fallback — otherwise
            // a default above the ceiling (e.g. the All role's 60) slips through
            // when no App Config key is set, violating the documented cap.
            var resolved = int.TryParse(configValue, out var parsed) && parsed > 0
                ? parsed
                : defaultPoolSize;
            return Math.Min(resolved, MaxAllowedPoolSize);
        }

        /// <summary>
        /// Sets the connection's <c>Application Name</c> to <c>sd{Service}[.{role}].{poolKind}</c>
        /// so <c>pg_stat_activity.application_name</c> distinguishes each service's data vs
        /// Hangfire pool (and, where a role is supplied, Worker vs Ingest pods).
        /// See docs/infrastructure/postgres-connection-budget.md §6.
        /// </summary>
        private static string ApplyApplicationName(string connString, string applicationName, string? role, string poolKind)
        {
            var svc = applicationName.Replace("SportsData.", string.Empty);
            // Omit the role segment when it just repeats the service name (e.g. the
            // Api service with role "Api") — produces sd{svc}.{poolKind} rather than
            // sd{svc}.{svc}.{poolKind}.
            var includeRole = !string.IsNullOrWhiteSpace(role)
                && !string.Equals(role, svc, StringComparison.OrdinalIgnoreCase);
            var tag = includeRole
                ? $"sd{svc}.{role}.{poolKind}"
                : $"sd{svc}.{poolKind}";

            // Drop any Application Name already on the base connection string, then set ours.
            connString = System.Text.RegularExpressions.Regex.Replace(
                connString,
                @"Application Name=[^;]*;?",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return $"{connString.TrimEnd(';')};Application Name={tag};";
        }

        public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration config)
        {
            var redisConnectionString = config[CommonConfigKeys.CacheServiceUri];

            if (string.IsNullOrWhiteSpace(redisConnectionString))
            {
                services.AddDistributedMemoryCache();
                return services;
            }

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;

                // TODO: Determine how to pass in an instance name from each consumer
                // i.e. sdApi, sdContest, sdVenue, etc.
                // (or have it generated here based on EnvironmentName and ApplicationName
                options.InstanceName = "sdapi_"; // (only one app using; good practice)
            });

            // Register IConnectionMultiplexer for direct Redis access (Lua scripts, etc.)
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(redisConnectionString));

            return services;
        }

        public static IServiceCollection AddDataPersistence<T>(
            this IServiceCollection services,
            IConfiguration configuration,
            string applicationName,
            Sport mode,
            int? maxPoolSize = null,
            string? role = null) where T : DbContext
        {
            // TODO: Clean up this hacky mess
            var cc = configuration.GetSection("CommonConfig")["SqlBaseConnectionString"];
            var connString = $"{cc};Database=sd{applicationName.Replace("SportsData.", string.Empty)}.{mode};Include Error Detail=true;";

            if (maxPoolSize.HasValue)
            {
                connString = System.Text.RegularExpressions.Regex.Replace(
                    connString,
                    @"Maximum Pool Size=\d+;?",
                    string.Empty,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                connString = $"{connString.TrimEnd(';')};Maximum Pool Size={maxPoolSize.Value};";
            }

            // Tag the data pool so pg_stat_activity.application_name distinguishes
            // it from the Hangfire pool (and, with role, Worker vs Ingest pods).
            connString = ApplyApplicationName(connString, applicationName, role, "Data");

            Console.WriteLine($"PostgreSQL: {connString}");

            services.AddDbContext<T>((serviceProvider, options) =>
            {
                // Only enable sensitive data logging in Development environment
                var env = serviceProvider.GetRequiredService<IHostEnvironment>();
                if (env.IsDevelopment())
                {
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                }
                
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
        /// Convenience wrapper for the single-pool services: resolves a clamped pool
        /// size (App Config key <c>{applicationName}:ConnectionPool:{poolConfigKey}</c>,
        /// falling back to <paramref name="defaultPoolSize"/>) and registers the
        /// DbContext with it. Keeps the leaf services from repeating the
        /// ResolvePoolSize + AddDataPersistence boilerplate.
        /// </summary>
        public static IServiceCollection AddDataPersistenceWithClampedPool<T>(
            this IServiceCollection services,
            IConfiguration configuration,
            string applicationName,
            Sport mode,
            string poolConfigKey = "Default",
            int defaultPoolSize = 10,
            string? role = null) where T : DbContext
        {
            var poolSize = ResolvePoolSize(configuration, applicationName, poolConfigKey, defaultPoolSize);
            return services.AddDataPersistence<T>(configuration, applicationName, mode, poolSize, role);
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
            services.AddSingleton<IValidateOptions<CommonConfig>, CommonConfigValidator>();
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
            Sport mode,
            bool includeServer = true,
            int? maxPoolSize = null,
            string[]? queues = null,
            string? role = null)
        {
            // TODO: Clean up this hacky mess
            var cc = configuration.GetSection("CommonConfig")["SqlBaseConnectionString"];
            var connString = $"{cc};Database=sd{applicationName.Replace("SportsData.", string.Empty)}.{mode}.Hangfire";

            if (maxPoolSize.HasValue)
            {
                connString = System.Text.RegularExpressions.Regex.Replace(
                    connString,
                    @"Maximum Pool Size=\d+;?",
                    string.Empty,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                connString = $"{connString.TrimEnd(';')};Maximum Pool Size={maxPoolSize.Value};";
            }

            // Tag the Hangfire pool distinctly from the data pool (see AddDataPersistence).
            connString = ApplyApplicationName(connString, applicationName, role, "Hangfire");

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
                }, new PostgreSqlStorageOptions
                {
                    // Long-running stream jobs (BaseballCompetitionStreamer, up to 5h
                    // per CompetitionStreamerBase.MaxStreamDuration) must outlive
                    // Hangfire's invisibility window or the storage layer re-fetches
                    // the "abandoned" job and cancels the in-flight worker via its
                    // linked CancellationToken. The default (30 min) caused every
                    // baseball live-stream to be reaped mid-game on 2026-05-21 (see
                    // Seq CorrelationId 1a80a1e6-02a3-4fa0-9ed9-291ed363beb9). Setting
                    // this above MaxStreamDuration with margin keeps streaming jobs
                    // owned by their original worker for the full game.
                    InvisibilityTimeout = TimeSpan.FromHours(6)
                });
                x.UseTagsWithPostgreSql();

                // Custom retry delays — longer than Hangfire defaults.
                // Dependencies (Coach, TeamSeason, etc.) may not be sourced yet;
                // faster retries just waste cycles and add noise to logs.
                // 1m, 2m, 5m, 10m, 20m, 40m, 1h, 2h, 4h, 8h
                x.UseFilter(new AutomaticRetryAttribute
                {
                    Attempts = 10,
                    DelaysInSeconds = [60, 120, 300, 600, 1200, 2400, 3600, 7200, 14400, 28800]
                });
            });

            if (includeServer)
            {
                services.AddHangfireServer(serverOptions =>
                {
                    serverOptions.WorkerCount = minWorkers;
                    serverOptions.ShutdownTimeout = TimeSpan.FromSeconds(90);

                    // Queue routing is role-driven (see Producer Program.cs). Daemon
                    // pods listen on "daemon" only; Worker pods listen on the default
                    // queue and (transitionally) "daemon" until streamers fully cut
                    // over per docs/contest-finalization-reconcile-backstop.md PR D.
                    if (queues is { Length: > 0 })
                    {
                        serverOptions.Queues = queues;
                    }
                });
            }

            return services;
        }

        public static IServiceCollection AddHealthChecks<TDbContext>(
            this IServiceCollection services,
            string apiName,
            Sport mode)
            where TDbContext : DbContext
        {
            services.AddHealthChecks()
                .AddCheck<HealthCheck>($"{apiName}-{mode}", tags: ["live", "ready"])
                .AddCheck<LoggingHealthCheck>("logging", tags: ["ready"])
                .AddCheck<DatabaseHealthCheck<TDbContext>>($"{apiName}-db", tags: ["ready"]);

            return services;
        }

        public static IServiceCollection AddHealthChecksMaster(
            this IServiceCollection services,
            string apiName)
        {
            services.AddHealthChecks()
                .AddCheck<HealthCheck>(apiName, tags: ["live", "ready"])
                //.AddCheck<CachingHealthCheck>("caching")
                .AddCheck<LoggingHealthCheck>("logging", tags: ["ready"]);
                //.AddCheck<ClientHealthCheck<IProvideContests>>(HttpClients.ContestClient)
                //.AddCheck<ClientHealthCheck<IProvideFranchises>>(HttpClients.FranchiseClient)
                //.AddCheck<ClientHealthCheck<IProvideNotifications>>(HttpClients.NotificationClient)
                //.AddCheck<ClientHealthCheck<IProvidePlayers>>(HttpClients.PlayerClient)
                //.AddCheck<ClientHealthCheck<IProvideProducers>>(HttpClients.ProducerClient)
                //.AddCheck<ClientHealthCheck<IProvideProviders>>(HttpClients.ProviderClient, tags: ["ready"]);
            //.AddCheck<ClientHealthCheck<IProvideSeasons>>(HttpClients.SeasonClient)
            //.AddCheck<ClientHealthCheck<IProvideVenues>>(HttpClients.VenueClient);

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
                            return !httpContext.Request.Path.StartsWithSegments("/api/health");
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

                    // Framework and application meters
                    builder.AddMeter(
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Server.Kestrel",
                        "System.Net.Http",
                        "SportsData.Provider.Espn",
                        "SportsData.Producer.Documents");

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
            var retrySection = configuration.GetSection("CommonConfig:Http:Retry");
            services.AddOptions<HttpRetryConfig>()
                .Bind(retrySection)
                .Validate(o => o.RetryCount >= 1 && o.BaseDelayMs >= 1,
                    "CommonConfig:Http:Retry: RetryCount and BaseDelayMs must be >= 1");

            // Eager read for the policy built at registration time. Normalize defensively in
            // case bad values slip past App Config (Validate above runs lazily on IOptions resolve).
            var retryConfig = retrySection.Get<HttpRetryConfig>() ?? new HttpRetryConfig();
            retryConfig.RetryCount = Math.Max(1, retryConfig.RetryCount);
            retryConfig.BaseDelayMs = Math.Max(1, retryConfig.BaseDelayMs);

            var registry = services.AddPolicyRegistry();
            registry.Add("HttpRetry", RetryPolicy.GetRetryPolicy(config: retryConfig));

            // Enables IHttpClientFactory for named clients
            services.AddHttpClient();

            /* ESPN */
            services.Configure<EspnApiClientConfig>(
                configuration.GetSection("SportsData.Provider:EspnApiClientConfig")
            );

            services.AddHttpClient<EspnHttpClient>(client =>
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; SportDeets/1.0; +https://sportdeets.com)");
                })
                .AddPolicyHandler(RetryPolicy.GetRetryPolicy(config: retryConfig));

            services.AddScoped<IProvideEspnApiData, EspnApiClient>();
            services.AddSingleton<IEspnCircuitBreaker, NoOpEspnCircuitBreaker>();
            services.AddSingleton<IEspnRateLimiter, NoOpEspnRateLimiter>();
            /* End ESPN */

            /* YouTube */
            services.Configure<YouTubeClientConfig>(
                configuration.GetSection("CommonConfig:YouTubeClientConfig"));

            services
                .AddHttpClient<IProvideYouTube, YouTubeHttpClient>(HttpClients.YouTubeClient,(sp, c) =>
                {
                    var config = sp.GetRequiredService<IOptions<YouTubeClientConfig>>().Value;
                    c.BaseAddress = new Uri("https://youtube.googleapis.com/youtube/v3/");
                    c.Timeout = TimeSpan.FromSeconds(15);
                    c.DefaultRequestVersion = HttpVersion.Version20;
                    c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                })
                .AddPolicyHandlerFromRegistry("HttpRetry");
            /* End YouTube */

            // Register single-mode services
            var providerApiUrl = configuration[CommonConfigKeys.GetProviderProviderUri()];
            if (!string.IsNullOrEmpty(providerApiUrl))
            {
                services
                    .AddHttpClient<IProvideProviders, ProviderClient>(HttpClients.ProviderClient, c =>
                    {
                        c.BaseAddress = new Uri(providerApiUrl);
                        c.Timeout = TimeSpan.FromSeconds(60);
                        c.DefaultRequestVersion = HttpVersion.Version20;
                        c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                    })
                    .AddPolicyHandlerFromRegistry("HttpRetry");
            }

            // Client factories handle resolution by sport/league mode
            services.AddSingleton<IVenueClientFactory, VenueClientFactory>();
            services.AddSingleton<IFranchiseClientFactory, FranchiseClientFactory>();
            services.AddSingleton<IContestClientFactory, ContestClientFactory>();
            services.AddSingleton<ISeasonClientFactory, SeasonClientFactory>();

            // Register mode-agnostic clients (same URL for all sports)
            var contestApiUrl = configuration[CommonConfigKeys.GetContestProviderUri()];
            if (!string.IsNullOrEmpty(contestApiUrl))
            {
                var contestClientName = $"{HttpClients.ContestClient}";
                services.AddHttpClient(contestClientName, client => client.BaseAddress = new Uri(contestApiUrl));
            }

            var venueApiUrl = configuration[CommonConfigKeys.GetVenueProviderUri()];
            if (!string.IsNullOrEmpty(venueApiUrl))
            {
                var venueClientName = $"{HttpClients.VenueClient}";
                services.AddHttpClient(venueClientName, client => client.BaseAddress = new Uri(venueApiUrl));
            }

            var franchiseApiUrl = configuration[CommonConfigKeys.GetFranchiseProviderUri()];
            if (!string.IsNullOrEmpty(franchiseApiUrl))
            {
                var franchiseClientName = $"{HttpClients.FranchiseClient}";
                services.AddHttpClient(franchiseClientName, client => client.BaseAddress = new Uri(franchiseApiUrl));
            }

            var seasonApiUrl = configuration[CommonConfigKeys.GetSeasonProviderUri()];
            if (!string.IsNullOrEmpty(seasonApiUrl))
            {
                var seasonClientName = $"{HttpClients.SeasonClient}";
                services.AddHttpClient(seasonClientName, client => client.BaseAddress = new Uri(seasonApiUrl));
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
