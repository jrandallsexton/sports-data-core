using Hangfire;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using SportsData.Api.Application;
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
            builder.UseCommon();

            Console.WriteLine("I AM NEW!");

            // Add services to the container.
            var config = builder.Configuration;
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName);

            var services = builder.Services;
            services.AddCoreServices(config);
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddProviders(config);
            services.AddMessaging(config, [typeof(HeartbeatConsumer)]);
            //services.AddInstrumentation(builder.Environment.ApplicationName);
            //services.AddHangfire(x => x.UseSqlServerStorage(config[$"{builder.Environment.ApplicationName}:ConnectionStrings:Hangfire"]));
            //services.AddCaching(config);
            //services.AddHealthChecksMaster(builder.Environment.ApplicationName);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //app.UseHttpsRedirection();

            //app.UseHangfireDashboard("/dashboard", new DashboardOptions
            //{
            //    Authorization = new[] { new DashboardAuthFilter() }
            //});

            app.UseAuthorization();

            //app.UseHealthChecks("/health", new HealthCheckOptions()
            //{
            //    ResponseWriter = HealthCheckWriter.WriteResponse
            //});

            app.MapControllers();

            var assemblyConfigurationAttribute = typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            var buildConfigurationName = assemblyConfigurationAttribute?.Configuration;

            app.UseCommonFeatures(buildConfigurationName);

            app.Run();
        }
    }
}
