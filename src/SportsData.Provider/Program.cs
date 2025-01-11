using Hangfire;

using SportsData.Core.DependencyInjection;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Config;
using SportsData.Provider.DependencyInjection;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn;
using SportsData.Provider.Middleware.Health;

using System.Reflection;

namespace SportsData.Provider
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.UseCommon();

            // Add services to the container.
            var config = builder.Configuration;
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName);

            var services = builder.Services;
            services.AddCoreServices(config);
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddProviders(config);

            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName);
            await services.ApplyMigrations<AppDataContext>();
            services.AddSingleton<DocumentService>();
            services.AddMessaging(config);

            services.AddHangfire(x => x.UseSqlServerStorage(config[$"{builder.Environment.ApplicationName}:ConnectionStrings:Hangfire"]));

            builder.Services.Configure<ProviderDocDatabase>(
                builder.Configuration.GetSection($"{builder.Environment.ApplicationName}:ProviderDocDatabase"));

            services.AddHangfireServer(serverOptions =>
            {
                // https://codeopinion.com/scaling-hangfire-process-more-jobs-concurrently/
                serverOptions.WorkerCount = 10;
            });

            services.AddHealthChecks<AppDataContext, Program>(Assembly.GetExecutingAssembly().GetName(false).Name);
            services.AddHealthChecks().AddCheck<DocumentDatabaseHealthCheck>(nameof(DocumentDatabaseHealthCheck));

            /* Hangfire Jobs */
            services.AddScoped<IProvideVenues, VenueProviderJob>();
            services.AddScoped<IProvideEspnApiData, EspnApiClient>();
            services.AddSingleton(new EspnApiClientConfig());

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //app.UseHttpsRedirection();

            app.UseHangfireDashboard("/dashboard", new DashboardOptions
            {
                Authorization = new[] { new DashboardAuthFilter() }
            });

            app.UseAuthorization();

            app.UseCommonFeatures();

            app.MapControllers();

            app.Services.ConfigureHangfireJobs();

            await app.RunAsync();
        }
    }
}
