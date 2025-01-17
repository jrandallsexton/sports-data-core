using SportsData.Core.DependencyInjection;
using SportsData.Season.Data;

using System.Reflection;

namespace SportsData.Season
{
    public class Program
    {
        public static void Main(string[] args)
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
            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName);
            services.AddMessaging(config);
            services.AddInstrumentation(builder.Environment.ApplicationName);
            services.AddHealthChecks<AppDataContext, Program>(builder.Environment.ApplicationName);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseCommonFeatures();

            app.MapControllers();

            app.Run();
        }
    }
}
