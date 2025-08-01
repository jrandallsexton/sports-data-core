using Hangfire;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Provider.Application.Documents;
using SportsData.Provider.Config;
using SportsData.Provider.DependencyInjection;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Seeders;
using SportsData.Provider.Middleware.Health;

namespace SportsData.Provider
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var mode = (args.Length > 0 && args[0] == "-mode") ?
                Enum.Parse<Sport>(args[1]) :
                Sport.All;

            Console.WriteLine($"Mode: {mode}");

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var config = builder.Configuration;
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName, mode);

            builder.UseCommon();

            var services = builder.Services;
            services.AddCoreServices(config, mode);
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddClients(config);
            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName, mode);
            services.AddHangfire(config, builder.Environment.ApplicationName, mode);

            services.AddMessaging(config, [typeof(DocumentRequestedHandler)]);

            services.AddInstrumentation(builder.Environment.ApplicationName);

            builder.Services.Configure<ProviderDocDatabaseConfig>(
                builder.Configuration.GetSection($"{builder.Environment.ApplicationName}:ProviderDocDatabaseConfig"));

            services.AddHealthChecks<AppDataContext, Program>(builder.Environment.ApplicationName, mode);
            services.AddHealthChecks().AddCheck<DocumentDatabaseHealthCheck>(nameof(DocumentDatabaseHealthCheck));

            var docDbProviderValue = config["SportsData.Provider:ProviderDocDatabaseConfig:Provider"];
            var useMongo = docDbProviderValue == "Mongo";
            services.AddLocalServices(mode, useMongo);

            var app = builder.Build();

            app.UseHttpsRedirection();

            // Apply migrations and seed data once using the real provider
            await app.Services.ApplyMigrations<AppDataContext>(ctx => LoadSeedData(ctx, mode));

            app.UseHangfireDashboard("/dashboard", new DashboardOptions
            {
                Authorization = [new DashboardAuthFilter()]
            });

            app.UseAuthorization();
            app.UseCommonFeatures();
            app.MapControllers();

            app.Services.ConfigureHangfireJobs(mode);

            await app.RunAsync();
        }


        private static async Task LoadSeedData(AppDataContext dbContext, Sport mode)
        {
            if (await dbContext.ResourceIndexJobs.AnyAsync())
                return;

            switch (mode)
            {
                case Sport.FootballNcaa:
                case Sport.FootballNfl:
                    // TODO: Move years to config
                    var footballValues = new FootballSeeder().Generate(mode, [2024]);
                    await dbContext.ResourceIndexJobs.AddRangeAsync(footballValues);
                    await dbContext.SaveChangesAsync();
                    break;
                case Sport.GolfPga:
                    var golfValues = new GolfSeeder().Generate(mode, [2024]);
                    await dbContext.ResourceIndexJobs.AddRangeAsync(golfValues);
                    await dbContext.SaveChangesAsync();
                    break;
                case Sport.BasketballNba:
                    var basketballValues = new BasketballSeeder().Generate(mode, [2024]);
                    await dbContext.ResourceIndexJobs.AddRangeAsync(basketballValues);
                    await dbContext.SaveChangesAsync();
                    break;
                case Sport.BaseballMlb:
                    break;
                case Sport.All:
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
    }
}
