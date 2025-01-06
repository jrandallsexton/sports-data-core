using SportsData.Core.Config;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Middleware;
using SportsData.Venue.Application.Handlers;
using SportsData.Venue.Infrastructure.Data;

using System.Reflection;

namespace SportsData.Venue
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var config = builder.Configuration;
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName);

            var services = builder.Services;
            services.Configure<CommonConfig>(config.GetSection("CommonConfig"));
            services.AddCoreServices(config);
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            // Add Serilog
            builder.UseCommon();

            services.AddProviders(config);
            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName);
            services.AddMessaging(config, [typeof(VenueCreatedHandler)]);
            services.AddHealthChecks<AppDataContext>(Assembly.GetExecutingAssembly().GetName(false).Name);

            var hostAssembly = Assembly.GetExecutingAssembly();
            builder.Services.AddAutoMapper(hostAssembly);
            services.AddMediatR(hostAssembly);

            // Apply Migrations
            await services.ApplyMigrations<AppDataContext>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseCommonFeatures();

            app.MapControllers();

            app.UseMiddleware<ExceptionHandlingMiddleware>();

            await app.RunAsync();
        }
    }
}
