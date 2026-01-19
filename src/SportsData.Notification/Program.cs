using SportsData.Core.DependencyInjection;
using SportsData.Notification.Infrastructure.Data;

using System.Reflection;
using SportsData.Core.Common;

namespace SportsData.Notification
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var config = builder.Configuration;
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName);

            builder.UseCommon();

            var services = builder.Services;
            services.AddCoreServices(config);
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName, Sport.All);
            services.AddMessaging(config, null);
            services.AddInstrumentation(builder.Environment.ApplicationName, config);
            services.AddHealthChecks<AppDataContext, Program>(builder.Environment.ApplicationName, Sport.All);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseCommonFeatures();

            app.MapControllers();

            var assemblyConfigurationAttribute = typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            var buildConfigurationName = assemblyConfigurationAttribute?.Configuration;

            app.UseCommonFeatures(buildConfigurationName);

            app.Run();
        }
    }
}
