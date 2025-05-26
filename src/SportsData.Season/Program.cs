using SportsData.Core.DependencyInjection;
using SportsData.Season.Data;

using System.Reflection;
using SportsData.Core.Common;

namespace SportsData.Season
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var mode = (args.Length > 0 && args[0] == "-mode") ?
                Enum.Parse<Sport>(args[1]) :
                Sport.All;

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

            services.AddClients(config);
            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName, mode);
            services.AddMessaging(config);
            services.AddInstrumentation(builder.Environment.ApplicationName);
            services.AddHealthChecks<AppDataContext, Program>(builder.Environment.ApplicationName, mode);

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
