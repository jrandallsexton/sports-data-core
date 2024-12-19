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
            services.AddScoped<IProvideVenues, VenueProvider>();

            services.AddHttpClient(HttpClients.VenueClient, client =>
            {
                client.BaseAddress = new Uri("http://localhost:5253/");
            });

            return services;
        }
    }
}
