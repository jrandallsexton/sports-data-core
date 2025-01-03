using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using SportsData.Api.Infrastructure;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Middleware.Health;

using System.Reflection;

namespace SportsData.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var config = builder.Configuration;
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName);

            var services = builder.Services;
            services.AddCoreServices(config);
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            // Add Serilog
            builder.UseCommon();

            services.AddProviders(config);
            services.AddMessaging(config, [typeof(HeartbeatConsumer)]);
            services.AddHealthChecksMaster(Assembly.GetExecutingAssembly().GetName(false).Name);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseHealthChecks("/health", new HealthCheckOptions()
            {
                ResponseWriter = HealthCheckWriter.WriteResponse
            });

            app.MapControllers();

            app.UseCommonFeatures();

            app.Run();
        }
    }
}
