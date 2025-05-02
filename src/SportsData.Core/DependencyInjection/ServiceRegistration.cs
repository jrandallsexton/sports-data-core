using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using SportsData.Core.Common;
using SportsData.Core.Config;
using SportsData.Core.Infrastructure.Blobs;
using SportsData.Core.Infrastructure.Clients;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.Notification;
using SportsData.Core.Infrastructure.Clients.Player;
using SportsData.Core.Infrastructure.Clients.Producer;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Infrastructure.Clients.Venue;
using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Linq;
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
            string applicationName) where T : DbContext
        {
            services.AddDbContext<T>(options =>
            {
                options.EnableSensitiveDataLogging();
                options.UseSqlServer(configuration[$"{applicationName}:ConnectionStrings:AppDataContext"]);
            });

            return services;
        }

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
        public static async Task<IServiceCollection> ApplyMigrations<T>(this IServiceCollection services, Func<T, Task> seedFunction = null) where T : DbContext
        {
            await using var serviceProvider = services.BuildServiceProvider();
            var context = serviceProvider.GetRequiredService<T>();
            var pending = await context.Database.GetPendingMigrationsAsync();
            if (pending.Any())
                await context.Database.MigrateAsync();

            //if (seedFunction is not null)
            //    await seedFunction(context);
            return services;
        }

        public static IServiceCollection AddCoreServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddSingleton<IProvideHashes, HashProvider>();
            services.AddScoped<IDecodeDocumentProvidersAndTypes, DocumentProviderAndTypeDecoder>();
            services.Configure<CommonConfig>(configuration.GetSection("CommonConfig"));
            services.AddScoped<IDateTimeProvider, DateTimeProvider>();
            return services;
        }

        public static IServiceCollection AddHealthChecks<TDbContext, TPublisher>(
            this IServiceCollection services,
            string apiName,
            string mode = "All")
            where TDbContext : DbContext where TPublisher : class
        {
            services.AddHealthChecks()
                .AddCheck<HealthCheck>($"{apiName}-{mode}")
                .AddCheck<LoggingHealthCheck>("logging")
                .AddCheck<DatabaseHealthCheck<TDbContext>>($"{apiName}-db");

            services.AddHostedService<HeartbeatPublisher<TPublisher>>();

            return services;
        }

        public static IServiceCollection AddHealthChecksMaster(this IServiceCollection services, string apiName)
        {
            services.AddHealthChecks()
                .AddCheck<HealthCheck>(apiName)
                .AddCheck<CachingHealthCheck>("caching")
                .AddCheck<LoggingHealthCheck>("logging")
                .AddCheck<ProviderHealthCheck<IProvideContests>>(HttpClients.ContestClient)
                .AddCheck<ProviderHealthCheck<IProvideFranchises>>(HttpClients.FranchiseClient)
                .AddCheck<ProviderHealthCheck<IProvideNotifications>>(HttpClients.NotificationClient)
                .AddCheck<ProviderHealthCheck<IProvidePlayers>>(HttpClients.PlayerClient)
                .AddCheck<ProviderHealthCheck<IProvideProducers>>(HttpClients.ProducerClient)
                .AddCheck<ProviderHealthCheck<IProvideProviders>>(HttpClients.ProviderClient)
                .AddCheck<ProviderHealthCheck<IProvideSeasons>>(HttpClients.SeasonClient)
                .AddCheck<ProviderHealthCheck<IProvideVenues>>(HttpClients.VenueClient);
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

        public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration config, List<Type> consumers = null)
        {
            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
                
                // TODO: Pass in the assembly and use: x.AddConsumers(assembly)
                consumers?.ForEach(z =>
                {
                    x.AddConsumer(z);
                });
                
                x.UsingAzureServiceBus((context, cfg) =>
                {
                    cfg.Host(config[CommonConfigKeys.AzureServiceBus]);
                    //cfg.ClearSerialization();
                    cfg.ConfigureJsonSerializerOptions(o =>
                    {
                        o.IncludeFields = true;
                        return o;
                    });
                    cfg.ConfigureEndpoints(context);
                });
            });
            return services;
        }

        public static IServiceCollection AddProviders(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddProvider<IProvideContests, ContestProvider>(configuration, HttpClients.ContestClient, CommonConfigKeys.ContestProviderUri)
                .AddProvider<IProvideFranchises, FranchiseProvider>(configuration, HttpClients.FranchiseClient, CommonConfigKeys.FranchiseProviderUri)
                .AddProvider<IProvideNotifications, NotificationProvider>(configuration, HttpClients.NotificationClient, CommonConfigKeys.NotificationProviderUri)
                .AddProvider<IProvidePlayers, PlayerProvider>(configuration, HttpClients.PlayerClient, CommonConfigKeys.PlayerProviderUri)
                .AddProvider<IProvideProducers, ProducerProvider>(configuration, HttpClients.ProducerClient, CommonConfigKeys.ProducerProviderUri)
                .AddProvider<IProvideProviders, ProviderProvider>(configuration, HttpClients.ProviderClient, CommonConfigKeys.ProviderProviderUri)
                .AddProvider<IProvideSeasons, SeasonProvider>(configuration, HttpClients.SeasonClient, CommonConfigKeys.SeasonProviderUri)
                .AddProvider<IProvideVenues, VenueProvider>(configuration, HttpClients.VenueClient, CommonConfigKeys.VenueProviderUri);
            return services;
        }

        private static IServiceCollection AddProvider<TService, TImplementation>(
            this IServiceCollection services,
            IConfiguration configuration,
            string providerName,
            string providerUrlKey) where TService : class
            where TImplementation : class, TService
        {
            var options = new ContestProviderConfig()
            {
                ApiUrl = configuration[providerUrlKey]
            };

            services.AddScoped<TService, TImplementation>();

            services.AddHttpClient(providerName, client =>
            {
                client.BaseAddress = new Uri(options.ApiUrl);
            });

            return services;
        }
    }
}
