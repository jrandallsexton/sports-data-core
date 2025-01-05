using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SportsData.Core.Common;
using SportsData.Core.Config;
using SportsData.Core.Infrastructure.Clients;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.Notification;
using SportsData.Core.Infrastructure.Clients.Player;
using SportsData.Core.Infrastructure.Clients.Producer;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Infrastructure.Clients.Venue;
using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using SportsData.Core.Infrastructure.Clients.Provider;

namespace SportsData.Core.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddDataPersistence<T>(
            this IServiceCollection services,
            IConfiguration configuration,
            string applicationName) where T : DbContext
        {
            services.AddDbContext<T>(options =>
            {
                options.EnableSensitiveDataLogging();
                //options.UseNpgsql(configuration.GetConnectionString("AppDataContext"));
                options.UseSqlServer(configuration[$"{applicationName}:ConnectionStrings:AppDataContext"]);
            });

            return services;
        }

        public static async Task<IServiceCollection> ApplyMigrations<T>(this IServiceCollection services) where T : DbContext
        {
            await using var serviceProvider = services.BuildServiceProvider();
            var context = serviceProvider.GetRequiredService<T>();
            await context.Database.MigrateAsync();

            return services;
        }

        public static IServiceCollection AddCoreServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<CommonConfig>(configuration.GetSection("CommonConfig"));
            services.AddScoped<IDateTimeProvider, DateTimeProvider>();
            return services;
        }

        public static IServiceCollection AddHealthChecks<TDbContext>(this IServiceCollection services, string apiName) where TDbContext : DbContext
        {
            services.AddHealthChecks()
                .AddCheck<HealthCheck>(apiName)
                .AddCheck<LoggingHealthCheck>("logging")
                .AddCheck<DatabaseHealthCheck<TDbContext>>($"{apiName}-db");

            return services;
        }

        public static IServiceCollection AddHealthChecksMaster(this IServiceCollection services, string apiName)
        {
            services.AddHealthChecks()
                .AddCheck<HealthCheck>(apiName)
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

                consumers?.ForEach(z =>
                {
                    x.AddConsumer(z);
                });
                
                x.UsingAzureServiceBus((context, cfg) =>
                {
                    cfg.Host(config[CommonConfigKeys.AzureServiceBus]);
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
