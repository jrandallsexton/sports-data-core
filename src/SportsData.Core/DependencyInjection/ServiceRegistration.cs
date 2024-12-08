using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SportsData.Core.Common;
using SportsData.Core.Middleware.Health;

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
    }
}
