using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.Notification;
using SportsData.Core.Infrastructure.Clients.Player;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Infrastructure.Clients.Venue;
using SportsData.Core.Middleware.Health;

using System;
using System.Reflection;
using SportsData.Core.Infrastructure.Clients.Producer;

namespace SportsData.Core.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddDataPersistence<T>(this IServiceCollection services, IConfiguration configuration) where T : DbContext
        {
            services.AddDbContext<T>(options =>
            {
                options.EnableSensitiveDataLogging();
                options.UseNpgsql(configuration.GetConnectionString("AppDataContext"));
            });

            return services;
        }

        public static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IDateTimeProvider, DateTimeProvider>();
            return services;
        }

        public static IServiceCollection AddHealthChecks(this IServiceCollection services, string apiName)
        {
            services.AddHealthChecks()
                .AddCheck<HealthCheck>(apiName);
            return services;
        }

        public static IServiceCollection AddHealthChecksMaster(this IServiceCollection services, string apiName)
        {
            services.AddHealthChecks()
                .AddCheck<HealthCheck>(apiName)
                .AddCheck<HealthCheckContest>(HttpClients.ContestClient)
                .AddCheck<HealthCheckFranchise>(HttpClients.FranchiseClient)
                .AddCheck<HealthCheckNotification>(HttpClients.NotificationClient)
                .AddCheck<HealthCheckPlayer>(HttpClients.PlayerClient)
                .AddCheck<HealthCheckProducer>(HttpClients.ProducerClient)
                .AddCheck<HealthCheckSeason>(HttpClients.SeasonClient)
                .AddCheck<HealthCheckVenue>(HttpClients.VenueClient);
            return services;
        }

        public static IServiceCollection AddMediatR(this IServiceCollection services, Assembly assembly)
        {
            services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));
            return services;
        }

        public static IServiceCollection AddProviders(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddContestProvider(configuration)
                .AddFranchiseProvider(configuration)
                .AddNotificationProvider(configuration)
                .AddPlayerProvider(configuration)
                .AddProducerProvider(configuration)
                .AddSeasonProvider(configuration)
                .AddVenueProvider(configuration);
            return services;
        }

        private static IServiceCollection AddContestProvider(this IServiceCollection services, IConfiguration configuration)
        {
            var options = new ContestProviderConfig()
            {
                ApiUrl = configuration.GetSection(nameof(ContestProviderConfig))["ApiUrl"]
            };

            services.AddScoped<IProvideContests, ContestProvider>();

            services.AddHttpClient(HttpClients.ContestClient, client =>
            {
                client.BaseAddress = new Uri(options.ApiUrl);
            });

            return services;
        }

        private static IServiceCollection AddFranchiseProvider(this IServiceCollection services, IConfiguration configuration)
        {
            var options = new FranchiseProviderConfig()
            {
                ApiUrl = configuration.GetSection(nameof(FranchiseProviderConfig))["ApiUrl"]
            };

            services.AddScoped<IProvideFranchises, FranchiseProvider>();

            services.AddHttpClient(HttpClients.FranchiseClient, client =>
            {
                client.BaseAddress = new Uri(options.ApiUrl);
            });

            return services;
        }

        private static IServiceCollection AddNotificationProvider(this IServiceCollection services, IConfiguration configuration)
        {
            var options = new NotificationProviderConfig()
            {
                ApiUrl = configuration.GetSection(nameof(NotificationProviderConfig))["ApiUrl"]
            };

            services.AddScoped<IProvideNotifications, NotificationProvider>();

            services.AddHttpClient(HttpClients.NotificationClient, client =>
            {
                client.BaseAddress = new Uri(options.ApiUrl);
            });

            return services;
        }

        private static IServiceCollection AddPlayerProvider(this IServiceCollection services, IConfiguration configuration)
        {
            var options = new PlayerProviderConfig()
            {
                ApiUrl = configuration.GetSection(nameof(PlayerProviderConfig))["ApiUrl"]
            };

            services.AddScoped<IProvidePlayers, PlayerProvider>();

            services.AddHttpClient(HttpClients.PlayerClient, client =>
            {
                client.BaseAddress = new Uri(options.ApiUrl);
            });

            return services;
        }

        private static IServiceCollection AddProducerProvider(this IServiceCollection services, IConfiguration configuration)
        {
            var options = new ProducerProviderConfig()
            {
                ApiUrl = configuration.GetSection(nameof(ProducerProviderConfig))["ApiUrl"]
            };

            services.AddScoped<IProvideProducers, ProducerProvider>();

            services.AddHttpClient(HttpClients.ProducerClient, client =>
            {
                client.BaseAddress = new Uri(options.ApiUrl);
            });

            return services;
        }

        private static IServiceCollection AddSeasonProvider(this IServiceCollection services, IConfiguration configuration)
        {
            var options = new SeasonProviderConfig()
            {
                ApiUrl = configuration.GetSection(nameof(SeasonProviderConfig))["ApiUrl"]
            };

            services.AddScoped<IProvideSeasons, SeasonProvider>();

            services.AddHttpClient(HttpClients.SeasonClient, client =>
            {
                client.BaseAddress = new Uri(options.ApiUrl);
            });

            return services;
        }

        private static IServiceCollection AddVenueProvider(this IServiceCollection services, IConfiguration configuration)
        {
            var options = new VenueProviderConfig()
            {
                ApiUrl = configuration.GetSection(nameof(VenueProviderConfig))["ApiUrl"]
            };

            services.AddScoped<IProvideVenues, VenueProvider>();

            services.AddHttpClient(HttpClients.VenueClient, client =>
            {
                client.BaseAddress = new Uri(options.ApiUrl);
            });

            return services;
        }
    }
}
