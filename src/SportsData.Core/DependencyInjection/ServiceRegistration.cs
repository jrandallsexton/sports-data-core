using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SportsData.Core.Common;
using SportsData.Core.Middleware.Health;

using System.Reflection;
using SportsData.Core.Infrastructure.Clients;
using SportsData.Core.Infrastructure.Clients.Venue;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.Notification;
using SportsData.Core.Infrastructure.Clients.Player;
using SportsData.Core.Infrastructure.Clients.Season;

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

            //services.AddScoped<IProvideSeasons, SeasonProvider>();
            //services.AddScoped<ProviderHealthCheck<SeasonProvider>>();

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
            services.AddContestProvider(configuration);
            services.AddFranchiseProvider(configuration);
            services.AddNotificationProvider(configuration);
            services.AddPlayerProvider(configuration);
            services.AddSeasonProvider(configuration);
            services.AddVenueProvider(configuration);
            return services;
        }

        private static IServiceCollection AddContestProvider(this IServiceCollection services, IConfiguration configuration)
        {
            var options = new ContestProviderConfig()
            {
                ApiUrl = configuration.GetSection("ContestProviderOptions")["ApiUrl"]
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
                ApiUrl = configuration.GetSection("FranchiseProviderOptions")["ApiUrl"]
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
                ApiUrl = configuration.GetSection("NotificationProviderOptions")["ApiUrl"]
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
                ApiUrl = configuration.GetSection("PlayerProviderOptions")["ApiUrl"]
            };

            services.AddScoped<IProvidePlayers, PlayerProvider>();

            services.AddHttpClient(HttpClients.PlayerClient, client =>
            {
                client.BaseAddress = new Uri(options.ApiUrl);
            });

            return services;
        }

        private static IServiceCollection AddSeasonProvider(this IServiceCollection services, IConfiguration configuration)
        {
            var options = new SeasonProviderConfig()
            {
                ApiUrl = configuration.GetSection("SeasonProviderOptions")["ApiUrl"]
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
                ApiUrl = configuration.GetSection("VenueProviderOptions")["ApiUrl"]
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
